using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;
using WebApplication2.Services;

namespace TodoApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class TodoController : ControllerBase
    {
        private readonly TodoService _todoService;

        public TodoController(TodoService todoService)
        {
            _todoService = todoService;
        }

        [HttpPost]
        public async Task<IActionResult> PostTodoItem([FromBody] TodoItem todoItem)
        {
            var result = await _todoService.AddTodo(todoItem.IsComplete, todoItem.Name ?? "");
            return result ? Ok() : BadRequest();
        }

        [HttpGet]
        public async Task<IActionResult> GetTodoItems()
        {
            var items = await _todoService.GetAllTodos();
            return Ok(items);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTodoItemById(long id)
        {
            var item = await _todoService.GetTodoItem(id);
            return item == null ? NotFound() : Ok(item);
        }

        [HttpPut("{taskName}")]
        public async Task<IActionResult> UpdateTodoItem(string taskName)
        {
            var result = await _todoService.UpdateTodoItem(taskName);
            return result ? NoContent() : NotFound();
        }

        [HttpDelete("{taskName}")]
        public async Task<IActionResult> DeleteTodoItem(string taskName)
        {
            var result = await _todoService.DeleteTodoItem(taskName);
            return result ? NoContent() : NotFound();
        }
    }
}