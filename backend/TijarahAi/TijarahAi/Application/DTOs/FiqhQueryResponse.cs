namespace TijarahAi.Application.DTOs;

public class FiqhPerspectiveResult
{
    public string Perspective { get; set; } = string.Empty; // "Hanafi" or "Ahl-e-Hadith"
    public string Ruling { get; set; } = string.Empty;
    public string Reasoning { get; set; } = string.Empty;
    public List<string> PermissibleConditions { get; set; } = new();
    public List<string> Prohibitions { get; set; } = new();
    public List<string> Citations { get; set; } = new();
}

public class FiqhQueryResponse
{
    public string Question { get; set; } = string.Empty;
    public FiqhPerspectiveResult HanafiPerspective { get; set; } = new();
    public FiqhPerspectiveResult AhleHadithPerspective { get; set; } = new();
    public string ComparativeSummary { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
