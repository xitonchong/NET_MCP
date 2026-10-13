using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

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
        if (string.IsNullOrWhiteSpace(message)) { yield return "Please enter a message."; yield break; }
        await foreach (var text in RunAgentAsync([new ChatMessage(ChatRole.User, message)], cancellationToken)) yield return text;
    }

    [HttpPost("chat")]
    public async IAsyncEnumerable<string> Chat([FromBody] AgentChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message)) { yield return "Please enter a message."; yield break; }
        var messages = (request.History ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Content)).TakeLast(10)
            .Select(item => new ChatMessage(item.Role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? ChatRole.Assistant : ChatRole.User, item.Content)).ToList();
        messages.Add(new ChatMessage(ChatRole.User, request.Message));
        await foreach (var text in RunAgentAsync(messages, cancellationToken)) yield return text;
    }

    private async IAsyncEnumerable<string> RunAgentAsync(IEnumerable<ChatMessage> messages, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var mcpClient = await McpClientHelper.CreateAsync(configuration, HttpContext, cancellationToken);
        var availableTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        var productTools = availableTools.Where(tool => tool.Name is "get_products" or "get_single_product").ToList();
        var agent = chatClient.AsAIAgent(instructions: Instructions, name: "CarvedRock Assistant", tools: [.. productTools]);
        await foreach (var update in agent.RunStreamingAsync(messages, cancellationToken: cancellationToken))
        {
            var text = update.ToString();
            if (!string.IsNullOrEmpty(text)) yield return text;
        }
    }
}
