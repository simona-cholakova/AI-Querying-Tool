using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TodoApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using WebApplication2.Services;


namespace TodoApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FilesController : ControllerBase
    {
        private readonly TodoContext _db;
        private readonly FileService _fileService;
        
        
        public FilesController(TodoContext db, UserManager<User> userManager,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _db = db;
            _fileService = new FileService(_db, embeddingGenerator);
        }

        [HttpPost("add-file")]
        public async Task<IActionResult> AddFile(IFormFile newFile)
        {
            if (newFile == null || string.IsNullOrWhiteSpace(newFile.FileName))
                return BadRequest("Invalid file data.");

            var exists = await _db.FileRecords.AnyAsync(f => f.FileName == newFile.FileName);
            if (exists)
                return BadRequest($"File '{newFile.FileName}' already exists.");

            var fileRecord = await _fileService.ProcessFileAsync(newFile);

            if (fileRecord == null)
                return BadRequest("Unsupported or invalid file format.");

            await _db.FileRecords.AddAsync(fileRecord);
            await _db.FileChunks.AddRangeAsync(fileRecord.Chunks);
            await _db.SaveChangesAsync();

            return Ok($"File '{newFile.FileName}' uploaded with {fileRecord.Chunks.Count} chunk(s).");
        }

        [HttpGet("get-file")]
        public async Task<IActionResult> GetFile([FromQuery] string fileName)
        {
            var file = await _db.FileRecords.FirstOrDefaultAsync(f => f.FileName == fileName);
            return file != null ? Ok(file) : BadRequest("Invalid file name.");
        }
    }

}