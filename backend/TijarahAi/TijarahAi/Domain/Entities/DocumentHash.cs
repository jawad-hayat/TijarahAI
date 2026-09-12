namespace TijarahAi.Domain.Entities;

public class DocumentHash
{
    public string FileName { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public int TotalChunks { get; set; }
    public DateTime LastIngestedUtc { get; set; } = DateTime.UtcNow;
}
