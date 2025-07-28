using System.ComponentModel;
using System.Security.Claims;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Seq.Api;

namespace TodoApi.Plugins;

public class SeqPlugin
{
    
    private readonly SeqConnection _conn = new SeqConnection("http://localhost:32768", "uk4JpNvfRyIinD5o5T6Y");
    private readonly ILogger<SeqPlugin> _logger;

    public SeqPlugin(ILogger<SeqPlugin> logger)
    {
        _logger = logger;
    }
    
    [KernelFunction("GetLogs")]
    [Description("Fetch the event from SEQ using the provided filters")]
    public async Task<IEnumerable<string>> QueryLogs(string filters, ClaimsPrincipal user)
    {
        Console.WriteLine(filters);    
        var res = _conn.Events.EnumerateAsync(filter: filters, render: true);        
        List<string> logs = new List<string>();
        await foreach (var evt in res)
        {
            Console.WriteLine(evt.RenderedMessage); 
            
            _logger.LogInformation("Getting logs based on filters");
            
            logs.Add(evt.RenderedMessage);
        }
        return logs;
    }
    
    [KernelFunction("GetTemplates")]
    [Description("Gets all the possible Message templates of the SEQ events")]
    public async Task<IEnumerable<string>> GetSEQMessageStructure()
    {
        var res = await _conn.Data.QueryAsync("select distinct(@MessageTemplate) as MessageTemplate from stream");
        List<string> messageTemplates = new List<string>();
        foreach (var row in res.Rows)
            messageTemplates.Add((string)row[0]);

        _logger.LogInformation("Getting all seq message templates");

        return messageTemplates;

    }

    [KernelFunction("Counting")]
    [Description("Counts the number of instances that match a specified parameter")]
    public async Task<IEnumerable<Object>> GetSeqLogsQuery(string query)
    {
        var result = await _conn.Data.QueryAsync(query); 
        _logger.LogInformation("Counting some instances...");

        return result.Rows;
    }
    
}