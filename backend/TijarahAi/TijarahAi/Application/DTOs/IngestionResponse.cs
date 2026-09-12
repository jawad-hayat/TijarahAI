namespace TijarahAi.Application.DTOs;

public class IngestionResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int HanafiChunksAdded { get; set; }
    public int AhleHadithChunksAdded { get; set; }
    public int TotalChunksInStore { get; set; }
    public List<string> SkippedFiles { get; set; } = new();
}
