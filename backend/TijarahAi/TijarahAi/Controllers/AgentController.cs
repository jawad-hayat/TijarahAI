using Microsoft.AspNetCore.Mvc;
using TijarahAi.Application.DTOs;
using TijarahAi.Application.Services;

namespace TijarahAi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly ContractRedlinerService _redlinerService;
    private readonly WebResearchAgentService _researchAgentService;

    public AgentController(ContractRedlinerService redlinerService, WebResearchAgentService researchAgentService)
    {
        _redlinerService = redlinerService;
        _researchAgentService = researchAgentService;
    }

    [HttpPost("audit-contract")]
    public async Task<ActionResult<ContractAuditResponse>> AuditContract([FromBody] ContractAuditRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ContractText))
            return BadRequest(new { error = "Contract text cannot be empty." });

        var response = await _redlinerService.AuditAndRedlineContractAsync(request, cancellationToken);
        return Ok(response);
    }

    [HttpPost("web-research")]
    public async Task<ActionResult<WebResearchResponse>> ConductWebResearch([FromBody] WebResearchRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequest(new { error = "Query cannot be empty." });

        try
        {
            var response = await _researchAgentService.ConductDeepWebResearchAsync(request.Query, cancellationToken);
            return Ok(response);
        }
        catch (InvalidOperationException exception) when (exception.Message.Contains("GEMINI_API_KEY", StringComparison.Ordinal))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = exception.Message });
        }
    }
}
