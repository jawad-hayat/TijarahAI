using Microsoft.EntityFrameworkCore;
using TijarahAi.Application.Common.Interfaces;
using TijarahAi.Application.Services;
using TijarahAi.Infrastructure.AI;
using TijarahAi.Infrastructure.Ingestion;
using TijarahAi.Infrastructure.MarketData;
using TijarahAi.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// 1. Add Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 2. CORS Policy for Angular Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 3. PostgreSQL Database Context (Supports Render Free PostgreSQL)
string? connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddDbContext<TijarahDbContext>(options =>
        options.UseNpgsql(connectionString, o => o.UseVector()));
}
else
{
    builder.Services.AddDbContext<TijarahDbContext>(options =>
        options.UseInMemoryDatabase("TijarahLocalFallback"));
}

// 4. HTTP Clients
builder.Services.AddHttpClient<IGeminiClient, GeminiApiClient>();
builder.Services.AddHttpClient<IStockDataProvider, YahooFinanceClient>();

// 5. Application & Infrastructure Singletons / Scoped Services
builder.Services.AddSingleton<IVectorStore, PersistentVectorStore>();
builder.Services.AddSingleton<IStockComplianceEngine, StockComplianceEngine>();
builder.Services.AddScoped<AgentRouterService>();
builder.Services.AddScoped<FiqhAdvisorService>();
builder.Services.AddScoped<DocumentIngestionService>();

var app = builder.Build();

// 6. HTTP Request Pipeline
if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TijarahAI API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors("AllowAll");

// 7. Serve Angular SPA from wwwroot if available
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();
app.MapControllers();

// 8. SPA fallback for client-side routing if index.html is present
if (!string.IsNullOrEmpty(app.Environment.WebRootPath) && 
    File.Exists(Path.Combine(app.Environment.WebRootPath, "index.html")))
{
    app.MapFallbackToFile("index.html");
}

// 9. Auto-initialize Vector Store & Trigger One-Time Embedding Check
using (var scope = app.Services.CreateScope())
{
    var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
    await vectorStore.InitializeAsync();

    // Check if initial ingestion is needed
    int existingChunks = await vectorStore.GetTotalChunksCountAsync();
    if (existingChunks == 0)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Vector store is empty on startup. Checking for knowledge base documents...");
        var ingestionService = scope.ServiceProvider.GetRequiredService<DocumentIngestionService>();
        
        string dataDir = Path.Combine(app.Environment.ContentRootPath, "data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(app.Environment.ContentRootPath, "..", "data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(app.Environment.ContentRootPath, "..", "..", "data");

        if (Directory.Exists(dataDir))
        {
            try
            {
                logger.LogInformation("Found data directory at {DataDir}. Triggering initial ingestion pipeline...", dataDir);
                await ingestionService.IngestDocumentsAsync(dataDir, forceRebuild: false);
            }
            catch (Exception ex)
            {
                logger.LogWarning("Initial ingestion skipped or deferred ({Message}). Ingestion can be triggered once GEMINI_API_KEY is configured.", ex.Message);
            }
        }
    }
}

app.Run();
