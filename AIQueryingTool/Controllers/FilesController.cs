using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using TodoApi.Models;
using WebApplication2.Services;

namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly FileService _fileService;

        public FilesController(TodoContext db, UserManager<User> userManager,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _fileService = new FileService(db, embeddingGenerator);
        }

        [HttpPost("add-file")]
        public async Task<IActionResult> AddFile(IFormFile newFile)
        {
            var result = await _fileService.HandleFileUploadAsync(newFile);
            return result.Success ? Ok(result.Message) : BadRequest(result.Message);
        }

        [HttpGet("get-file")]
        public async Task<IActionResult> GetFile([FromQuery] string fileName)
        {
            var result = await _fileService.FetchFileByNameAsync(fileName);
            return result.Success ? Ok(result.File) : BadRequest(result.Message);
        }
    }
}