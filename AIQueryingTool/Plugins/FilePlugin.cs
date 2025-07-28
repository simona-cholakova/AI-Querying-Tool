using Microsoft.SemanticKernel;
using System.ComponentModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using TodoApi.Models;
using Pgvector.EntityFrameworkCore;
using WebApplication2.Services;

namespace TodoApi.Plugins
{
    public class FilePlugin
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly TodoContext _context;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly TodoService _todoService;
        private readonly ILogger<FilePlugin> _logger;

        public FilePlugin(IServiceProvider serviceProvider, TodoContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, ILogger<FilePlugin> logger)
        {
            _serviceProvider = serviceProvider;
            _context = context;
            _embeddingGenerator = embeddingGenerator;
            _logger = logger;
        }
        
        [KernelFunction, Description("Searches if the user's prompt can be found in the files from the database. Always read from the most similar file.")]
        public async Task<List<FileChunk>> searchFileContent(string query)
        {
            Console.WriteLine("Function invoked yay!");

            var embedding = await _embeddingGenerator.GenerateAsync(query);
            var queryVector = new Pgvector.Vector(embedding.Vector);

            // Only consider chunks that have been clustered
            var closestChunks = _context.FileChunks
                .Where(c => c.Embedding != null) // This is fine
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector)) // Order by distance
                .Take(5) // Take the 5 closest
                .ToList();

            _logger.LogInformation("searchFileContent invoked");
            
            return closestChunks;
        }

        
    }
}