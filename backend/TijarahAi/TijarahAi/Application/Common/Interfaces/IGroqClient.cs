namespace TijarahAi.Application.Common.Interfaces;

public interface IGroqClient
{
    string ModelName { get; }
    Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default);
}
