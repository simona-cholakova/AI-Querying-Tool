using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Services;

[ApiController]
[Route("api/[controller]")]
public class PromptController : ControllerBase
{
    private readonly PromptService _promptService;

    public PromptController(PromptService promptService)
    {
        _promptService = promptService;
    }

    [Authorize]
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] string inputText)
        => Ok(await _promptService.HandleChat(inputText, User));

    [Authorize]
    [HttpPost("logs")]
    public async Task<IActionResult> HandleSeqLogs([FromBody] string inputText)
        => Ok(await _promptService.HandleLogs(inputText, User));

    [Authorize]
    [HttpPost("aboutDatabase")]
    public async Task<IActionResult> HandleMcpToolQuery([FromBody] string inputText)
        => Ok(await _promptService.HandleMcp(inputText, User));

    [Authorize]
    [HttpPost("todos")]
    public async Task<IActionResult> HandleTodos([FromBody] string inputText)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return userId == null ? Unauthorized() : Ok(await _promptService.HandleTodos(inputText, User));
    }

    [Authorize]
    [HttpPost("gitCommits")]
    public async Task<IActionResult> HandleGitCommits([FromBody] string inputText)
        => Ok(await _promptService.HandleGitCommits(inputText, User));

    [Authorize]
    [HttpPost("rules")]
    public async Task<IActionResult> HandleRules([FromBody] string inputText)
        => Ok(await _promptService.HandleRules(inputText, User));
}