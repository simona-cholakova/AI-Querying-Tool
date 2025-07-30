using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TodoApi.Plugins;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using TodoApi.Models;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors;
using WebApplication2.Services;

public class ToDoPlugin
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TodoContext _context;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly TodoService _todoService;
    private readonly ILogger<ToDoPlugin> _logger;
    
    public ToDoPlugin(IServiceProvider serviceProvider, TodoContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, TodoService todoService, ILogger<ToDoPlugin> logger)
    {
        _serviceProvider = serviceProvider;
        _context = context;
        _embeddingGenerator = embeddingGenerator;
        _todoService = todoService;
        _logger = logger;
    }
    
    [KernelFunction("GetAllTodos"), Description("This function gets all todo tasks")]
    public async Task<string> GetAllTodoTasksAsync(string userId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TodoContext>();

        var tasks = await db.ToDoItems.Where(t => t.UserId == userId).ToListAsync(); // ✅ Filter by userId
        if (tasks.Count == 0) return "You have no todo tasks.";

        _logger.LogInformation("GetAllTodosAsync invoked");

        var result = string.Join("\n", tasks.Select(t => $"- {t.Name}"));
        return result;
    }


    [KernelFunction("createTodo")]
    [Description("Creates a new todo for the current user. Returns true if the task was successfully added")]
    public async Task<bool> addTodoItem(string task, bool isComplete, string userId)
    {
        if (await _todoService.AddTodo(isComplete, task, userId))
        {
            return true;
        }

        _logger.LogInformation("CreateToDo invoked");
        return false;
    }

    
    [KernelFunction, Description("Deletes a todo task from database")]
    public async Task<bool> deleteToDoItem(string task)
    {
        if (await _todoService.DeleteTodoItem(task))
        {
            return true;
        }

        _logger.LogInformation("deleteToDoItem invoked");

        return false;
    }
    
    [KernelFunction, Description("Updates a todo task in the database")]
    public async Task<bool> updateToDoItem(string task)
    {
        if (await _todoService.UpdateTodoItem(task))
        {
            return true;
        }

        _logger.LogInformation("updateToDoItem invoked");

        return false;
    }
}