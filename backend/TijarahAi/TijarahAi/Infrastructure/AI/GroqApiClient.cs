using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TijarahAi.Application.Common.Interfaces;

namespace TijarahAi.Infrastructure.AI;

public class GroqApiClient : IGroqClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger<GroqApiClient> _logger;

    public string ModelName => _model;

    public GroqApiClient(HttpClient httpClient, IConfiguration config, ILogger<GroqApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // Groq API key is retrieved via environment variable GROQ_API_KEY
        string? apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = config["GROQ_API_KEY"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = config["Groq:ApiKey"];

        _apiKey = apiKey?.Trim() ?? string.Empty;

        // Model resides in appsettings (e.g. "Groq:Model")
        _model = (config["Groq:Model"] ?? "llama-3.3-70b-versatile").Trim();
    }

    public async Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            throw new InvalidOperationException("GROQ_API_KEY environment variable is not configured.");
        }

        const string url = "https://api.groq.com/openai/v1/chat/completions";

        var messages = new List<object>();

        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }

        messages.Add(new { role = "user", content = userPrompt });

        var payload = new
        {
            model = _model,
            messages,
            temperature = 0.1
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        _logger.LogInformation("Dispatching text generation request to Groq Cloud using model '{Model}'.", _model);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Groq Cloud text generation failed ({(StatusCode)} {ReasonPhrase}): {ResponseJson}",
                (int)response.StatusCode, response.ReasonPhrase, responseJson);
            throw new HttpRequestException(
                $"Groq text generation failed ({(int)response.StatusCode} {response.ReasonPhrase}): {responseJson}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Groq text generation returned no choices in response.");
        }

        var firstChoice = choices[0];
        if (firstChoice.TryGetProperty("message", out var messageElement) &&
            messageElement.TryGetProperty("content", out var contentElement))
        {
            return contentElement.GetString() ?? string.Empty;
        }

        throw new InvalidOperationException("Groq text generation returned an invalid message format.");
    }
}
