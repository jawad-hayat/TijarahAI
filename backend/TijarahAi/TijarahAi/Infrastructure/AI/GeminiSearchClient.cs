using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;
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
    private readonly string _generationModel;
    private readonly ILogger<GeminiSearchClient> _logger;
    private readonly IGroqClient _groqClient;

    public GeminiSearchClient(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiSearchClient> logger,
        IGroqClient groqClient)
    {
        _httpClient = httpClient;
        _logger = logger;
        _groqClient = groqClient;
        
        string? apiKey = config["GEMINI_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = config["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        _apiKey = apiKey?.Trim() ?? string.Empty;
        _generationModel = (config["Gemini:GenerationModel"] ?? "gemini-2.5-flash").Trim();
    }

    public async Task<(string ResponseText, List<ResearchSource> Sources)> GenerateWithWebSearchAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException(
                "Web research requires GEMINI_API_KEY. Configure it with 'dotnet user-secrets set GEMINI_API_KEY <your-key>' or an environment variable.");
        }

        string model = _generationModel.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? _generationModel
            : $"models/{_generationModel}";
        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";

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
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests ||
                (int)response.StatusCode == 429 ||
                json.Contains("RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) ||
                json.Contains("429", StringComparison.Ordinal))
            {
                _logger.LogWarning("Gemini Web Search returned 429 Too Many Requests / RESOURCE_EXHAUSTED. Falling back to Groq Cloud ({Model}).", _groqClient.ModelName);
                string groqText = await _groqClient.GenerateTextAsync(
                    "You are a web-grounded comparative fiqh research agent for TijarahAI.",
                    prompt,
                    cancellationToken);
                return (groqText, new List<ResearchSource>());
            }

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
