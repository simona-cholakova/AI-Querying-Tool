using AIQueryingTool;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TodoApi.Models;
using TodoApi.Utils;

namespace TodoApi.Controllers
{
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
        private readonly ILogger<PromptController> _logger;

        public PromptController(
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
        }

        // General Chat Prompt
        [Authorize]
        [HttpPost("chat")]
        public async Task<IActionResult> Chat([FromBody] string inputText)
        {
            var chatHistory = await _kernelUtils.BuildChatHistory(inputText, User);
            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            }, _kernel);

            _logger.LogInformation("/chat endpoint reached");
            await _kernelUtils.SaveHistory(inputText, result[0].Content, User);
            return Ok(result[0].Content);
        }
        
        //SEQ
        [Authorize]
        [HttpPost("logs")]
        public async Task<IActionResult> HandleSeqLogs([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemMessages.SystemMessageForSplunkSeq());
            chatHistory.AddUserMessage(inputText);

            KernelFunction getLogs = _kernel.Plugins.GetFunction("SeqPlugin", "GetLogs");
            KernelFunction getTemplates = _kernel.Plugins.GetFunction("SeqPlugin", "GetTemplates");
            KernelFunction searchFiles = _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent");
    
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [getTemplates, getLogs, searchFiles])
            };

            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);

            _logger.LogInformation("/logs endpoint reached");
            
            await _kernelUtils.SaveHistory(inputText, result[0].Content, User);

            return Ok(result[0].Content);
        }


        // ToDos Query
        [Authorize]
        [HttpPost("todos")]
        public async Task<IActionResult> HandleTodos([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemMessages.SystemMessageForTodos());
            chatHistory.AddUserMessage(inputText);

            KernelFunction getTodos = _kernel.Plugins.GetFunction("ToDoPlugin", "GetAllTodos");
            KernelFunction createTodo = _kernel.Plugins.GetFunction("ToDoPlugin", "createTodo");
            KernelFunction deleteToDo = _kernel.Plugins.GetFunction("ToDoPlugin", "deleteToDoItem");

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [getTodos, createTodo, deleteToDo])
            };

            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);

            _logger.LogInformation("/todos endpoint reached");
            
            await _kernelUtils.SaveHistory(inputText, result[0].Content, User);

            return Ok(result[0].Content);
        }



        // Rules-Based Logic
        [Authorize]
        [HttpPost("rules")]
        public async Task<IActionResult> HandleRules([FromBody] string inputText)
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(SystemMessages.SystemMessageForRules());
            chatHistory.AddUserMessage(inputText);
    
            KernelFunction searchFiles = _kernel.Plugins.GetFunction("FilePlugin", "searchFileContent");

            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(functions: [searchFiles])
            };
    
            var result = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);

            _logger.LogInformation("/rules endpoint reached");

            await _kernelUtils.SaveHistory(inputText, result[0].Content, User);

            return Ok(result[0].Content);
        }

        
    }
}
