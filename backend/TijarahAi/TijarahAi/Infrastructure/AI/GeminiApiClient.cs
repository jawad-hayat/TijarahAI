using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;

namespace TijarahAi.Infrastructure.AI;

public class GeminiApiClient : IGeminiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiApiClient> _logger;
    private readonly Lazy<Task<string>> _embeddingModel;
    private readonly Lazy<Task<string>> _generationModel;

    public GeminiApiClient(HttpClient httpClient, IConfiguration config, ILogger<GeminiApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = config["GEMINI_API_KEY"] 
            ?? config["Gemini:ApiKey"] 
            ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY") 
            ?? throw new InvalidOperationException("GEMINI_API_KEY is not configured in appsettings.json or environment variables.");

        var configuredEmbeddingModel = config["Gemini:EmbeddingModel"] ?? "text-embedding-004";
        _embeddingModel = new Lazy<Task<string>>(
            () => ResolveEmbeddingModelAsync(configuredEmbeddingModel));

        var configuredGenerationModel = config["Gemini:GenerationModel"] ?? "gemini-3.6-flash";
        _generationModel = new Lazy<Task<string>>(
            () => ResolveGenerationModelAsync(configuredGenerationModel));
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
    {
        string model = await _embeddingModel.Value.WaitAsync(cancellationToken);
        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:embedContent?key={Uri.EscapeDataString(_apiKey)}";

        var payload = new
        {
            model,
            content = new
            {
                parts = new[] { new { text } }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var values = doc.RootElement
            .GetProperty("embedding")
            .GetProperty("values");

        var result = new float[values.GetArrayLength()];
        int i = 0;
        foreach (var val in values.EnumerateArray())
        {
            result[i++] = val.GetSingle();
        }

        return result;
    }

    public async Task<List<float[]>> GenerateBatchEmbeddingsAsync(List<string> texts, CancellationToken cancellationToken = default)
    {
        string model = await _embeddingModel.Value.WaitAsync(cancellationToken);
        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:batchEmbedContents?key={Uri.EscapeDataString(_apiKey)}";

        var requests = texts.Select(t => new
        {
            model,
            content = new { parts = new[] { new { text = t } } }
        }).ToList();

        var payload = new { requests };
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(json);
        var embeddingsArray = doc.RootElement.GetProperty("embeddings");

        var results = new List<float[]>();
        foreach (var emb in embeddingsArray.EnumerateArray())
        {
            var values = emb.GetProperty("values");
            var floatArr = new float[values.GetArrayLength()];
            int j = 0;
            foreach (var val in values.EnumerateArray())
            {
                floatArr[j++] = val.GetSingle();
            }
            results.Add(floatArr);
        }

        return results;
    }

    private async Task<string> ResolveEmbeddingModelAsync(string configuredModel)
    {
        string configuredResourceName = NormalizeModelName(configuredModel);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(_apiKey)}";

        using var response = await _httpClient.GetAsync(url);
        string responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Unable to list Gemini models ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var availableModels = document.RootElement.TryGetProperty("models", out var models)
            ? models.EnumerateArray()
                .Where(model => SupportsEmbedding(model))
                .Select(model => model.GetProperty("name").GetString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList()
            : new List<string>();

        string? selectedModel = availableModels.FirstOrDefault(
            model => string.Equals(model, configuredResourceName, StringComparison.OrdinalIgnoreCase));

        selectedModel ??= availableModels.FirstOrDefault();
        if (selectedModel is null)
        {
            throw new InvalidOperationException(
                $"No Gemini embedding model is available for this API key. Configured model: {configuredResourceName}.");
        }

        _logger.LogInformation("Using Gemini embedding model {EmbeddingModel}.", selectedModel);
        return selectedModel;
    }

    private static bool SupportsEmbedding(JsonElement model)
    {
        if (!model.TryGetProperty("name", out var name) || name.GetString() is null ||
            !model.TryGetProperty("supportedGenerationMethods", out var methods))
        {
            return false;
        }

        return methods.EnumerateArray().Any(method =>
            string.Equals(method.GetString(), "embedContent", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method.GetString(), "batchEmbedContents", StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeModelName(string modelName)
    {
        modelName = modelName.Trim();
        return modelName.StartsWith("models/", StringComparison.OrdinalIgnoreCase)
            ? modelName
            : $"models/{modelName}";
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        string model = await _generationModel.Value.WaitAsync(cancellationToken);
        string url = $"https://generativelanguage.googleapis.com/v1beta/{model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";

        var payload = new
        {
            system_instruction = new
            {
                parts = new[] { new { text = systemPrompt } }
            },
            contents = new[]
            {
                new { parts = new[] { new { text = userPrompt } } }
            },
            generationConfig = new
            {
                temperature = 0.1,
                topK = 20,
                topP = 0.8
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Gemini text generation failed ({(int)response.StatusCode} {response.ReasonPhrase}): {responseJson}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates))
            throw new InvalidOperationException("Gemini text generation returned no candidates.");

        foreach (var candidate in candidates.EnumerateArray())
        {
            if (!candidate.TryGetProperty("content", out var candidateContent) ||
                !candidateContent.TryGetProperty("parts", out var parts))
            {
                continue;
            }

            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var text))
                    return text.GetString() ?? string.Empty;
            }
        }

        throw new InvalidOperationException("Gemini text generation returned no text content.");
    }

    private async Task<string> ResolveGenerationModelAsync(string configuredModel)
    {
        string configuredResourceName = NormalizeModelName(configuredModel);
        string url = $"https://generativelanguage.googleapis.com/v1beta/models?key={Uri.EscapeDataString(_apiKey)}";

        using var response = await _httpClient.GetAsync(url);
        string responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Unable to list Gemini models ({(int)response.StatusCode} {response.ReasonPhrase}): {responseBody}");
        }

        using var document = JsonDocument.Parse(responseBody);
        var availableModels = document.RootElement.TryGetProperty("models", out var models)
            ? models.EnumerateArray()
                .Where(SupportsTextGeneration)
                .Select(model => model.GetProperty("name").GetString())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .ToList()
            : new List<string>();

        string? selectedModel = availableModels.FirstOrDefault(
            model => string.Equals(model, configuredResourceName, StringComparison.OrdinalIgnoreCase));

        selectedModel ??= availableModels.FirstOrDefault();
        if (selectedModel is null)
        {
            throw new InvalidOperationException(
                $"No Gemini text-generation model is available for this API key. Configured model: {configuredResourceName}.");
        }

        _logger.LogInformation("Using Gemini text-generation model {GenerationModel}.", selectedModel);
        return selectedModel;
    }

    private static bool SupportsTextGeneration(JsonElement model)
    {
        return model.TryGetProperty("name", out var name) && name.GetString() is not null &&
            model.TryGetProperty("supportedGenerationMethods", out var methods) &&
            methods.EnumerateArray().Any(method =>
                string.Equals(method.GetString(), "generateContent", StringComparison.OrdinalIgnoreCase));
    }
}
