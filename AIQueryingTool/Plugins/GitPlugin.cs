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
    
    [KernelFunction("GetCommitDiff")]
    [Description("Gets the file changes (diff) for a specific Git commit SHA")]
    public async Task<string> GetCommitDiff(string repoPath, string sha)
    {
        if (!Repository.IsValid(repoPath))
            return $"The provided path '{repoPath}' is not a valid Git repository.";

        var sb = new StringBuilder();

        try
        {
            using var repo = new Repository(repoPath);
            var commit = repo.Commits.FirstOrDefault(c => c.Sha.StartsWith(sha, StringComparison.OrdinalIgnoreCase));

            if (commit == null)
                return $"No commit found with SHA starting with '{sha}'.";

            if (!commit.Parents.Any())
                return "This is the root commit and does not have any parents.";

            var parent = commit.Parents.First();
            var changes = repo.Diff.Compare<Patch>(parent.Tree, commit.Tree);

            foreach (var change in changes)
            {
                sb.AppendLine($"File: {change.Path}");
                sb.AppendLine($"Status: {change.Status}");
            
                foreach (var addedLine in change.AddedLines)
                {
                    sb.AppendLine($"+ {addedLine}");
                }

                foreach (var deletedLine in change.DeletedLines)
                {
                    sb.AppendLine($"- {deletedLine}");
                }

                sb.AppendLine(new string('-', 50));
            }

            _logger.LogInformation("GetCommitDiff invoked for SHA: {Sha}", sha);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while getting commit diff for SHA: {Sha}", sha);
            return $"An error occurred while retrieving the commit diff: {ex.Message}";
        }

        return sb.ToString();
    }

    
  
}