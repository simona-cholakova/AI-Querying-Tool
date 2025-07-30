using System.Text;
using System.Text.Json;
using Accord.MachineLearning;
using Accord.Math.Distances;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using TodoApi.Models;
using TodoApi.Utils;
using UglyToad.PdfPig;

namespace WebApplication2.Services
{
    public class FileService
    {
        private readonly TodoContext _context;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        public FileService(TodoContext context, IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _context = context;
            _embeddingGenerator = embeddingGenerator;
        }
        
        public async Task<(bool Success, string Message)> HandleFileUploadAsync(IFormFile newFile)
        {
            if (newFile == null || string.IsNullOrWhiteSpace(newFile.FileName))
                return (false, "Invalid file data.");

            var exists = await _context.FileRecords.AnyAsync(f => f.FileName == newFile.FileName);
            if (exists)
                return (false, $"File '{newFile.FileName}' already exists.");

            var fileRecord = await ProcessFileAsync(newFile);
            if (fileRecord == null)
                return (false, "Unsupported or invalid file format.");

            await _context.FileRecords.AddAsync(fileRecord);
            await _context.FileChunks.AddRangeAsync(fileRecord.Chunks);
            await _context.SaveChangesAsync();

            return (true, $"File '{newFile.FileName}' uploaded with {fileRecord.Chunks.Count} chunk(s).");
        }

        public async Task<(bool Success, string Message, FileRecord? File)> FetchFileByNameAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return (false, "File name is required.", null);

            var file = await _context.FileRecords.FirstOrDefaultAsync(f => f.FileName == fileName);

            return file != null
                ? (true, "File retrieved.", file)
                : (false, "File not found.", null);
        }
        
        public static string FlattenJson(JsonElement element)
        {
            var sb = new StringBuilder();

            void Recurse(JsonElement el)
            {
                switch (el.ValueKind)
                {
                    case JsonValueKind.Object:
                        foreach (var property in el.EnumerateObject())
                        {
                            sb.Append(property.Name).Append(": ");
                            Recurse(property.Value);
                            sb.AppendLine();
                        }
                        break;

                    case JsonValueKind.Array:
                        foreach (var item in el.EnumerateArray())
                        {
                            Recurse(item);
                            sb.AppendLine();
                        }
                        break;

                    case JsonValueKind.String:
                        sb.Append(el.GetString());
                        break;

                    case JsonValueKind.Number:
                        sb.Append(el.GetRawText());
                        break;

                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        sb.Append(el.GetBoolean());
                        break;

                    case JsonValueKind.Null:
                        sb.Append("null");
                        break;
                }
            }

            Recurse(element);
            return sb.ToString();
        }

        public static List<string> SplitTextIntoChunks(string text, int maxChars)
        {
            var chunks = new List<string>();

            for (int i = 0; i < text.Length; i += maxChars)
            {
                int length = Math.Min(maxChars, text.Length - i);
                chunks.Add(text.Substring(i, length));
            }

            return chunks;
        }

        public void KMeansClustering(FileRecord uploadedFile)
        {
            var chunksList = uploadedFile.Chunks.ToList();

            if (chunksList.Count == 0)
                return;

            // Convert embeddings to double[][] for Accord
            double[][] embeddingsForClustering = chunksList
                .Select(c => c.Embedding.ToArray().Select(f => (double)f).ToArray())
                .ToArray();

            int numberOfClusters = Math.Min(100, chunksList.Count);

            // Initialize and train K-Means
            var kmeans = new KMeans(k: numberOfClusters)
            {
                Distance = new Euclidean(),
                MaxIterations = 100
            };

            var clusters = kmeans.Learn(embeddingsForClustering);
            int[] assignments = clusters.Decide(embeddingsForClustering);

            // Assign cluster info to each chunk
            for (int i = 0; i < chunksList.Count; i++)
            {
                chunksList[i].ClusterID = assignments[i];
                chunksList[i].ClusterMethod = $"K-Means (k={numberOfClusters})";
            }

            // Save changes to the database
            _context.SaveChanges();
        }
        
        public async Task ProcessBatch(List<string> batch, FileRecord fileRecord)
        {
            var embeddings = await _embeddingGenerator.GenerateAndZipAsync(batch);

            foreach (var embedding in embeddings)
            {
                ReadOnlyMemory<float> embeddingMemory = embedding.Embedding.Vector;
                Pgvector.Vector vector = new Pgvector.Vector(embeddingMemory);

                fileRecord.Chunks.Add(new FileChunk
                {
                    Content = embedding.Value,
                    Embedding = vector
                });
            }
        }
        
        public int EstimateTokenCount(string text)
        {
            return (int)(text.Length / 4.0); // very rough estimate: 1 token ≈ 4 characters
        }
        
        public async Task<FileRecord?> ProcessFileAsync(IFormFile file)
        {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileRecord = new FileRecord
        {
            FileName = file.FileName,
            Content = string.Empty,
            Chunks = new List<FileChunk>()
        };

        if (extension == ".pdf")
        {
            using var pdf = PdfDocument.Open(file.OpenReadStream());
            var fullText = new StringBuilder();
            int pageNum = 1;

            foreach (var page in pdf.GetPages())
            {
                string pageText = page.Text;
                fullText.AppendLine(pageText);

                var chunks = SplitTextIntoChunks(pageText, 2000);
                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingGenerator.GenerateAsync(chunk);
                    fileRecord.Chunks.Add(new FileChunk
                    {
                        PageNumber = pageNum,
                        Content = chunk,
                        Embedding = new Vector(embedding.Vector.ToArray())
                    });
                }

                pageNum++;
            }

            fileRecord.Content = fullText.ToString();
        }
        else if (extension == ".json")
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8, true);
            var jsonText = await reader.ReadToEndAsync();

            try
            {
                var jsonDoc = JsonDocument.Parse(jsonText);
                var flattened = FlattenJson(jsonDoc.RootElement);
                var chunks = SplitTextIntoChunks(flattened, 2000);

                int pageNum = 1;
                foreach (var chunk in chunks)
                {
                    var embedding = await _embeddingGenerator.GenerateAsync(chunk);
                    fileRecord.Chunks.Add(new FileChunk
                    {
                        PageNumber = pageNum++,
                        Content = chunk,
                        Embedding = new Vector(embedding.Vector.ToArray())
                    });
                }

                fileRecord.Content = flattened;
            }
            catch (JsonException)
            {
                return null;
            }
        }
        else if (extension == ".jsonl")
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);
            string? line;
            List<string> batch = new();
            int tokenCount = 0;
            const int maxTokens = 7000;

            var fullText = new StringBuilder();

            while ((line = await reader.ReadLineAsync()) != null)
            {
                string content = JsonLSplitter.ExtractValuesOnly(line);
                fullText.AppendLine(content);

                int tokens = EstimateTokenCount(content);
                if (tokenCount + tokens > maxTokens && batch.Count > 0)
                {
                    await ProcessBatch(batch, fileRecord);
                    batch.Clear();
                    tokenCount = 0;
                }

                batch.Add(content);
                tokenCount += tokens;
            }

            if (batch.Count > 0)
            {
                await ProcessBatch(batch, fileRecord);
            }

            fileRecord.Content = fullText.ToString();
        }
        else if (extension == ".txt")
        {
            using var reader = new StreamReader(file.OpenReadStream());
            var text = await reader.ReadToEndAsync();
            var chunks = SplitTextIntoChunks(text, 2000);

            int pageNum = 1;
            foreach (var chunk in chunks)
            {
                var embedding = await _embeddingGenerator.GenerateAsync(chunk);
                fileRecord.Chunks.Add(new FileChunk
                {
                    PageNumber = pageNum++,
                    Content = chunk,
                    Embedding = new Vector(embedding.Vector.ToArray())
                });
            }

            fileRecord.Content = text;
        }
        else
        {
            return null;
        }

        return fileRecord;
    }

    }
}