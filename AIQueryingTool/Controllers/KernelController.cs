using System.Security.Claims;
using AIQueryingTool;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TodoApi.Models;
using TodoApi.Utils;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PromptController : ControllerBase
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly UserManager<User> _userManager;
    private readonly TodoContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IServiceProvider _serviceProvider;
    private readonly KernelUtils _kernelUtils;
    private readonly KernelService _kernelService;
    private readonly ILogger<PromptController> _logger;

    public PromptController(
        KernelService kernelService,
        Kernel kernel,
        IChatCompletionService chatService,
        UserManager<User> userManager,
        TodoContext context,
        IServiceProvider serviceProvider,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        KernelUtils kernelUtils,
        ILogger<PromptController> logger)
    {
        _kernel = kernel;
        _chatCompletionService = chatService;
        _userManager = userManager;
        _context = context;
        _embeddingGenerator = embeddingGenerator;
        _serviceProvider = serviceProvider;
        _kernelUtils = kernelUtils;
        _logger = logger;
        _kernelService  = kernelService;
    }

    [Authorize]
    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] string inputText)
    {
        var chatHistory = await _kernelUtils.BuildChatHistory(inputText, User, SystemMessages.SystemMessageForEverything());
        var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        }, _kernel);

        _logger.LogInformation("/chat endpoint reached");
        await _kernelUtils.SaveHistory(inputText, result[0].Content, User);
        return Ok(result[0].Content);
    }

    [Authorize]
    [HttpPost("logs")]
    public async Task<IActionResult> HandleSeqLogs([FromBody] string inputText)
    {
        var chatHistory = await _kernelUtils.BuildChatHistory(inputText, User, SystemMessages.SystemMessageForSplunkSeq());
        
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("SeqPlugin", "GetTemplates"),
                _kernel.Plugins.GetFunction("SeqPlugin", "GetLogs"),
                _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent"),
                /*
                _kernel.Plugins.GetFunction("McpToolPlugin", "query")
            */
            })
        };

        var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
        _logger.LogInformation("/logs endpoint reached");
        await _kernelUtils.SaveHistory(inputText, result[0].Content, User);
        return Ok(result[0].Content);
    }
    
    
    [Authorize]
    [HttpPost("aboutDatabase")]
    public async Task<IActionResult> HandleMcpToolQuery([FromBody] string inputText)
    {
        var chatHistory = await _kernelUtils.BuildChatHistory(inputText, User, SystemMessages.SystemMessageForMcpQuery());
    
        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("McpToolPlugin", "query")
            })
        };
    
        var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
        _logger.LogInformation("/aboutDatabase endpoint reached");
        await _kernelUtils.SaveHistory(inputText, result[0].Content, User);
        return Ok(result[0].Content);
    }


    [Authorize]
    [HttpPost("todos")]
    public async Task<IActionResult> HandleTodos([FromBody] string inputText)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId == null) return Unauthorized();

        var enhancedInput = $"{inputText}\nUserId: {userId}";

        var chatHistory = await _kernelUtils.BuildChatHistory(enhancedInput, User, SystemMessages.SystemMessageForTodos());

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("ToDoPlugin", "GetAllTodos"),
                _kernel.Plugins.GetFunction("ToDoPlugin", "createTodo"),
                _kernel.Plugins.GetFunction("ToDoPlugin", "deleteToDoItem")
            })
        };

        var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
        _logger.LogInformation("/todos endpoint reached");

        return Ok(result[0].Content);

    }

    [Authorize]
    [HttpPost("gitCommits")]
    public async Task<IActionResult> HandleGitCommits([FromBody] string inputText)
    {
        var chatHistory = await _kernelUtils.BuildChatHistory(inputText, User, SystemMessages.SystemMessageForGitCommits());

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("GitPlugin", "GetGitCommits"),
                _kernel.Plugins.GetFunction("GitPlugin", "GetCommitDiff"),
            })
        };

        var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
        var response = result[0].Content;

        _logger.LogInformation("/gitCommits endpoint reached");
        await _kernelUtils.SaveHistory(inputText, response, User);
        return Ok(response);
    }

    [Authorize]
    [HttpPost("rules")]
    public async Task<IActionResult> HandleRules([FromBody] string inputText)
    {
        var chatHistory = await _kernelUtils.BuildChatHistory(inputText, User, SystemMessages.SystemMessageForRules());

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent")
            })
        };

        var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
        _logger.LogInformation("/rules endpoint reached");
        await _kernelUtils.SaveHistory(inputText, result[0].Content, User);
        return Ok(result[0].Content);
    }
}
