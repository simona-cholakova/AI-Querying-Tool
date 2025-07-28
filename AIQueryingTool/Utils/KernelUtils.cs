using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel.ChatCompletion;
using TodoApi.Models;
using System.Security.Claims;
using AIQueryingTool;

namespace TodoApi.Utils;

public class KernelUtils
{
    
    private readonly UserManager<User> _userManager;
    private readonly TodoContext _context;
    private readonly string _systemMessage;

    public KernelUtils(TodoContext context, UserManager<User> userManager)
    {
        _userManager = userManager;
        _context = context;
    }
    
    public async Task<ChatHistory> BuildChatHistory(string inputText, ClaimsPrincipal user, string systemMessage)
    {
        var chatHistory = new ChatHistory();
        chatHistory.AddSystemMessage(systemMessage); 
        if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        string userId = _userManager.GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Unable to retrieve user ID.");
        }

        var userChat = await _context.UserContextHistory
            .Where(h => h.userId == userId)
            .OrderByDescending(h => h.Id)
            .Take(10)
            .ToListAsync();

        userChat.Reverse();
        foreach (var prompt in userChat)
        {
            chatHistory.AddUserMessage(prompt.userPrompt);
            chatHistory.AddAssistantMessage(prompt.agentResponse);
        }

        chatHistory.AddUserMessage(inputText);
        return chatHistory;
    }


    public async Task SaveHistory(string inputText, string response, ClaimsPrincipal user)
    {
        if (!(user?.Identity?.IsAuthenticated ?? false))
        {
            throw new InvalidOperationException("User is not authenticated.");
        }

        string userId = _userManager.GetUserId(user);
        if (string.IsNullOrEmpty(userId))
        {
            throw new InvalidOperationException("Unable to retrieve user ID.");
        }

        var userContextHistory = new UserContextHistory
        {
            userPrompt = inputText,
            userId = userId,
            agentResponse = response
        };

        _context.UserContextHistory.Add(userContextHistory);
        await _context.SaveChangesAsync();
    }


}