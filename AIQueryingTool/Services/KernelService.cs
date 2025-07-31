using System.Security.Claims;
using AIQueryingTool;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using TodoApi.Models;
using TodoApi.Utils;

namespace TodoApi.Services;

public class PromptService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly UserManager<User> _userManager;
    private readonly KernelUtils _kernelUtils;
    private readonly ILogger<PromptService> _logger;

    public PromptService(
        Kernel kernel,
        IChatCompletionService chatService,
        UserManager<User> userManager,
        KernelUtils kernelUtils,
        ILogger<PromptService> logger)
    {
        _kernel = kernel;
        _chatService = chatService;
        _userManager = userManager;
        _kernelUtils = kernelUtils;
        _logger = logger;
    }

    public async Task<string> HandleChat(string inputText, ClaimsPrincipal user)
    {
        var history = await _kernelUtils.BuildChatHistory(inputText, user, SystemMessages.SystemMessageForEverything());
        var result = await _chatService.GetChatMessageContentsAsync(history, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        }, _kernel);
        await _kernelUtils.SaveHistory(inputText, result[0].Content, user);
        _logger.LogInformation("/chat handled");
        return result[0].Content;
    }

    public async Task<string> HandleLogs(string inputText, ClaimsPrincipal user)
    {
        var history = await _kernelUtils.BuildChatHistory(inputText, user, SystemMessages.SystemMessageForSplunkSeq());
        var result = await _chatService.GetChatMessageContentsAsync(history, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("SeqPlugin", "GetTemplates"),
                _kernel.Plugins.GetFunction("SeqPlugin", "GetLogs"),
                _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent")
            })
        }, _kernel);
        await _kernelUtils.SaveHistory(inputText, result[0].Content, user);
        _logger.LogInformation("/logs handled");
        return result[0].Content;
    }

    public async Task<string> HandleMcp(string inputText, ClaimsPrincipal user)
    {
        var history = await _kernelUtils.BuildChatHistory(inputText, user, SystemMessages.SystemMessageForMcpQuery());
        var result = await _chatService.GetChatMessageContentsAsync(history, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("McpToolPlugin", "query")
            })
        }, _kernel);
        await _kernelUtils.SaveHistory(inputText, result[0].Content, user);
        _logger.LogInformation("/aboutDatabase handled");
        return result[0].Content;
    }

    public async Task<string> HandleTodos(string inputText, ClaimsPrincipal user)
    {
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var enhancedInput = $"{inputText}\nUserId: {userId}";
        var history = await _kernelUtils.BuildChatHistory(enhancedInput, user, SystemMessages.SystemMessageForTodos());

        var result = await _chatService.GetChatMessageContentsAsync(history, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("ToDoPlugin", "GetAllTodos"),
                _kernel.Plugins.GetFunction("ToDoPlugin", "createTodo"),
                _kernel.Plugins.GetFunction("ToDoPlugin", "deleteToDoItem")
            })
        }, _kernel);
        _logger.LogInformation("/todos handled");
        return result[0].Content;
    }

    public async Task<string> HandleGitCommits(string inputText, ClaimsPrincipal user)
    {
        var history = await _kernelUtils.BuildChatHistory(inputText, user, SystemMessages.SystemMessageForGitCommits());

        var result = await _chatService.GetChatMessageContentsAsync(history, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("GitPlugin", "GetGitCommits"),
                _kernel.Plugins.GetFunction("GitPlugin", "GetCommitDiff"),
            })
        }, _kernel);
        await _kernelUtils.SaveHistory(inputText, result[0].Content, user);
        _logger.LogInformation("/gitCommits handled");
        return result[0].Content;
    }

    public async Task<string> HandleRules(string inputText, ClaimsPrincipal user)
    {
        var history = await _kernelUtils.BuildChatHistory(inputText, user, SystemMessages.SystemMessageForRules());

        var result = await _chatService.GetChatMessageContentsAsync(history, new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(new[]
            {
                _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent")
            })
        }, _kernel);
        await _kernelUtils.SaveHistory(inputText, result[0].Content, user);
        _logger.LogInformation("/rules handled");
        return result[0].Content;
    }
}
