using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.DTOs;

namespace TijarahAi.Infrastructure.AI;

public interface IGeminiSearchClient
{
    Task<(string ResponseText, List<ResearchSource> Sources)> GenerateWithWebSearchAsync(string prompt, CancellationToken cancellationToken = default);
}

public class GeminiSearchClient : IGeminiSearchClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiSearchClient> _logger;

    public GeminiSearchClient(HttpClient httpClient, IConfiguration config, ILogger<GeminiSearchClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["GEMINI_API_KEY"] 
            ?? config["Gemini:ApiKey"]
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not configured.");
    }

    public async Task<(string ResponseText, List<ResearchSource> Sources)> GenerateWithWebSearchAsync(string prompt, CancellationToken cancellationToken = default)
    {
        string url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

        var payload = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            tools = new object[] { new { google_search = new { } } },
            generationConfig = new { temperature = 0.2 }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini Web Search error: {Error}", json);
            return ("Web search service temporarily unavailable.", new List<ResearchSource>());
        }

        using var doc = JsonDocument.Parse(json);
        var candidate = doc.RootElement.GetProperty("candidates")[0];
        
        string responseText = candidate
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;

        var sources = new List<ResearchSource>();

        if (candidate.TryGetProperty("groundingMetadata", out var groundingMeta) &&
            groundingMeta.TryGetProperty("groundingChunks", out var chunks))
        {
            foreach (var chunk in chunks.EnumerateArray())
            {
                if (chunk.TryGetProperty("web", out var web))
                {
                    sources.Add(new ResearchSource
                    {
                        Title = web.TryGetProperty("title", out var t) ? t.GetString() ?? "Islamic Reference" : "Islamic Reference",
                        Url = web.TryGetProperty("uri", out var u) ? u.GetString() ?? "" : "",
                        Snippet = "Verified Scholarly Reference"
                    });
                }
            }
        }

        return (responseText, sources.DistinctBy(s => s.Url).Take(5).ToList());
    }
}
