using Microsoft.AspNetCore.Mvc;
using TijarahAi.Application.DTOs;
using TijarahAi.Application.Services;

namespace TijarahAi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FiqhController : ControllerBase
{
    private readonly FiqhAdvisorService _fiqhAdvisorService;
    private readonly AgentRouterService _agentRouter;
    private readonly ILogger<FiqhController> _logger;

    public FiqhController(
        FiqhAdvisorService fiqhAdvisorService,
        AgentRouterService agentRouter,
        ILogger<FiqhController> logger)
    {
        _fiqhAdvisorService = fiqhAdvisorService;
        _agentRouter = agentRouter;
        _logger = logger;
    }

    [HttpPost("ask")]
    public async Task<ActionResult<FiqhQueryResponse>> AskFiqhQuestion([FromBody] FiqhQueryRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { error = "Question cannot be empty." });

        _logger.LogInformation("Processing business fiqh inquiry: {Question}", request.Question);

        // Execute dual-madhab RAG retrieval & synthesis
        var response = await _fiqhAdvisorService.AnswerComparativeFiqhQueryAsync(request.Question, cancellationToken);
        return Ok(response);
    }
}
