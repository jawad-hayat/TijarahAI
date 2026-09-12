using System.Text.Json.Serialization;
using TijarahAi.Domain.Enums;

namespace TijarahAi.Domain.Entities;

public class KnowledgeChunk
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Madhab Madhab { get; set; }
    public string DocumentSource { get; set; } = string.Empty;
    public string StandardOrBook { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string SectionTitle { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Citations { get; set; } = string.Empty;
    public string DocumentHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Persisted vector representation (768-dim float array from Gemini text-embedding-004)
    public float[] Embedding { get; set; } = Array.Empty<float>();
}
