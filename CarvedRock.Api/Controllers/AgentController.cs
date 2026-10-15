using System.Text;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CarvedRock.Api.Controllers;

public sealed record AgentMessage(string Role, string Content);
public sealed record AgentChatRequest(string Message, IReadOnlyList<AgentMessage>? History);

[ApiController]
[Route("[controller]")]
[Authorize]
public sealed class AgentController(IConfiguration configuration, IChatClient chatClient) : ControllerBase
{
    private const string Instructions = """
        You are an assistant that recommends CarvedRock products.
        Use the available product tools to obtain current product information.
        Recommend no more than 3 products for any request.
        Do not invent products, prices, or product details.
        If you cannot help with a request, say so politely.
        """;

    [HttpGet]
    public async IAsyncEnumerable<string> Get([FromQuery] string message, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            yield return "Please enter a message.";
            yield break;
        }

        var isAdminMode = IsAdminCommand(message);

        if (isAdminMode && !User.IsInRole("admin"))
        {
            yield return
                "You must be an administrator to use admin mode.";

            yield break;
        }

        var cleanedMessage = RemoveAdminPrefix(message);

        var messages = new[]
        {
            new ChatMessage(ChatRole.User, cleanedMessage)
        };

        await foreach (var text in RunAgentAsync(
            messages,
            isAdminMode,
            cancellationToken))
        {
            yield return text;
        }
    }

    [HttpPost("chat")]
    public async IAsyncEnumerable<string> Chat([FromBody] AgentChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            yield return "Please enter a message.";
            yield break;
        }

        var isAdminMode = IsAdminCommand(request.Message);

        if (isAdminMode && !User.IsInRole("admin"))
        {
            yield return "You must be an administrator to use admin mode.";
            yield break;
        }

        var messages = (request.History ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Content))
            .TakeLast(10)
            .Select(item =>
            {
                var isAssistant = item.Role.Equals(
                    "assistant",
                    StringComparison.OrdinalIgnoreCase);

                return new ChatMessage(
                    isAssistant ? ChatRole.Assistant : ChatRole.User,
                    isAssistant
                        ? item.Content
                        : RemoveAdminPrefix(item.Content));
            })
            .ToList();

        messages.Add(new ChatMessage(
            ChatRole.User,
            RemoveAdminPrefix(request.Message)));

        await foreach (var text in RunAgentAsync(
            messages,
            isAdminMode,
            cancellationToken))
        {
            yield return text;
        }
    }

    private async IAsyncEnumerable<string> RunAgentAsync(
        IEnumerable<ChatMessage> messages,
        bool isAdminMode,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var mcpClient = await McpClientHelper.CreateAsync(configuration, HttpContext, cancellationToken);
        var availableTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

        var instructions = Instructions;
        var selectedTools = availableTools
            .Where(tool => tool.Name is "get_products" or "get_single_product")
            .ToList();

        if (isAdminMode)
        {
            instructions = await GetAdminInstructionsAsync(
                mcpClient,
                cancellationToken);

            selectedTools = availableTools.ToList();
        }

        var agent = chatClient.AsAIAgent(
            instructions: instructions,
            name: isAdminMode
                ? "CarvedRock Administrator Assistant"
                : "CarvedRock Assistant",
            tools: [.. selectedTools]);

        await foreach (var update in agent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            var text = update.ToString();
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }

    private static async Task<string> GetAdminInstructionsAsync(
        McpClient mcpClient,
        CancellationToken cancellationToken)
    {
        var prompt = await mcpClient.GetPromptAsync(
            "admin_prompt",
            cancellationToken: cancellationToken);

        var instructions = new StringBuilder();

        foreach (var message in prompt.Messages)
        {
            if (message.Content is TextContentBlock textContent)
            {
                instructions.AppendLine(textContent.Text);
            }
        }

        if (instructions.Length == 0)
        {
            throw new InvalidOperationException(
                "The admin_prompt did not return any text.");
        }

        return instructions.ToString();
    }

    private static bool IsAdminCommand(string message)
    {
        if (!message.StartsWith("/admin", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return message.Length == "/admin".Length ||
            char.IsWhiteSpace(message["/admin".Length]);
    }

    private static string RemoveAdminPrefix(string message)
    {
        if (!IsAdminCommand(message))
        {
            return message;
        }
        return message["/admin".Length..].Trim();
    }
}
