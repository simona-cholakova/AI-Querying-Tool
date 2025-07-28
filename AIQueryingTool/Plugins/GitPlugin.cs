using System.ComponentModel;
using System.Text;
using LibGit2Sharp;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

public class GitPlugin
{
    private readonly ILogger<GitPlugin> _logger;

    public GitPlugin(ILogger<GitPlugin> logger)
    {
        _logger = logger;
    }
    
    [KernelFunction("GetGitCommits")]
    [Description("Gets the history of the git commits")]
    public async Task<string> GetGitCommits(string repoPath, int count = 10)
    {
        if (!Repository.IsValid(repoPath))
            return $"The provided path '{repoPath}' is not a valid Git repository.";

        var sb = new StringBuilder();

        using (var repo = new Repository(repoPath))
        {
            var commits = repo.Commits.Take(count);

            foreach (var commit in commits)
            {
                sb.AppendLine($"Commit: {commit.Sha.Substring(0, 7)}");
                sb.AppendLine($"Author: {commit.Author.Name} <{commit.Author.Email}>");
                sb.AppendLine($"Date: {commit.Author.When.UtcDateTime:u}");
                sb.AppendLine($"Message: {commit.MessageShort}");
                sb.AppendLine(new string('-', 50));
            }
        }

        _logger.LogInformation("GetGitCommits invoked");
        
        return sb.ToString();
    }
    
  
}