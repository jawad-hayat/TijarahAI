using System.ComponentModel.DataAnnotations;

namespace TijarahAi.Application.DTOs;

public class FiqhQueryRequest
{
    [Required]
    public string Question { get; set; } = string.Empty;
}
