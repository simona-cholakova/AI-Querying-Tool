using System.Security.Claims;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using TodoApi.Utils;

public class KernelService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly KernelUtils _kernelUtils;

    public KernelService(Kernel kernel, IChatCompletionService chatService, KernelUtils kernelUtils)
    {
        _kernel = kernel;
        _chatService = chatService;
        _kernelUtils = kernelUtils;
    }

    public async Task<string> HandlePromptAsync(
        string inputText,
        ClaimsPrincipal user,
        string systemMessage,
        IEnumerable<KernelFunction>? functions = null)
    {
        var chatHistory = await _kernelUtils.BuildChatHistory(inputText, user, systemMessage);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = functions != null
                ? FunctionChoiceBehavior.Auto(functions.ToArray())
                : FunctionChoiceBehavior.Auto()
        };

        var result = await _chatService.GetChatMessageContentsAsync(chatHistory, settings, _kernel);
        var response = result[0].Content;

        await _kernelUtils.SaveHistory(inputText, response, user);
        return response;
    }

    public static float CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length) return 0f;

        float dot = 0f, magA = 0f, magB = 0f;

        for (int i = 0; i < vectorA.Length; i++)
        {
            dot += vectorA[i] * vectorB[i];
            magA += vectorA[i] * vectorA[i];
            magB += vectorB[i] * vectorB[i];
        }

        return dot / (float)(Math.Sqrt(magA) * Math.Sqrt(magB) + 1e-8f);
    }
}