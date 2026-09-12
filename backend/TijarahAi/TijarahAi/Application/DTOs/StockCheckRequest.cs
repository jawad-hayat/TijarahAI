using System.ComponentModel.DataAnnotations;

namespace TijarahAi.Application.DTOs;

public class StockCheckRequest
{
    [Required]
    public string Ticker { get; set; } = string.Empty;
}
