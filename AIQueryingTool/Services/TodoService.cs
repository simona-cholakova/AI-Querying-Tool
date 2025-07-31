using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;

namespace WebApplication2.Services
{
    public class TodoService
    {
        private readonly TodoContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TodoService(TodoContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        private string GetUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            return user?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                ?? throw new InvalidOperationException("User not authenticated");
        }

        public async Task<IEnumerable<TodoItem>> GetAllTodos()
        {
            var userId = GetUserId();
            return await _context.ToDoItems
                .Where(t => t.UserId == userId)
                .ToListAsync();
        }

        public async Task<TodoItem?> GetTodoItem(long todoId)
        {
            var userId = GetUserId();
            return await _context.ToDoItems
                .FirstOrDefaultAsync(t => t.Id == todoId && t.UserId == userId);
        }

        public async Task<bool> AddTodo(bool isComplete, string task)
        {
            var userId = GetUserId();
            return await AddTodo(isComplete, task, userId);
        }

        public async Task<bool> AddTodo(bool isComplete, string task, string userId)
        {
            var todoItem = new TodoItem
            {
                Name = task,
                IsComplete = isComplete,
                UserId = userId
            };

            _context.ToDoItems.Add(todoItem);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteTodoItem(string taskName)
        {
            var userId = GetUserId();
            var todoItem = await _context.ToDoItems
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Name != null && t.Name.Contains(taskName));

            if (todoItem == null)
            {
                return false;
            }

            _context.ToDoItems.Remove(todoItem);
            return await _context.SaveChangesAsync() > 0;
        }

        public async Task<bool> UpdateTodoItem(string taskName)
        {
            var userId = GetUserId();
            var todoItem = await _context.ToDoItems
                .FirstOrDefaultAsync(t => t.UserId == userId && t.Name != null && t.Name.Contains(taskName));

            if (todoItem == null)
            {
                return false;
            }

            todoItem.IsComplete = true;
            return await _context.SaveChangesAsync() > 0;
        }
        
    }
}
