using Microsoft.AspNetCore.Mvc;
using TijarahAi.Application.DTOs;
using TijarahAi.Infrastructure.Ingestion;

namespace TijarahAi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class IngestionController : ControllerBase
{
    private readonly DocumentIngestionService _ingestionService;
    private readonly IWebHostEnvironment _env;

    public IngestionController(DocumentIngestionService ingestionService, IWebHostEnvironment env)
    {
        _ingestionService = ingestionService;
        _env = env;
    }

    [HttpPost("trigger")]
    public async Task<ActionResult<IngestionResponse>> TriggerIngestion([FromQuery] bool force = false, CancellationToken cancellationToken = default)
    {
        // Resolve data directory
        string dataDir = Path.Combine(_env.ContentRootPath, "data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(_env.ContentRootPath, "..", "data");
        if (!Directory.Exists(dataDir))
            dataDir = Path.Combine(_env.ContentRootPath, "..", "..", "data");

        var result = await _ingestionService.IngestDocumentsAsync(dataDir, forceRebuild: force, cancellationToken);
        return Ok(result);
    }
}
