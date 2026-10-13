using System.Runtime.CompilerServices; 
using Microsoft.AspNetCore.Authorization; 
using Microsoft.AspNetCore.Mvc; 
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;


namespace CarvedRock.Api.Controllers; 


[ApiController]
[Route("[controller]")]
[Authorize]
public sealed class AgentController( 
    IConfiguration configuration, 
    IChatClient chatClient
): ControllerBase
{
    private const string Instructions = 
    """
    You are an assistant that recommends CarvedRock products. 

    Use the available product tools to obtain current product information. 
    Recommend no more than 3 products for any requests. 
    Do not invent products, prices or product details. 
    if you cannot help with a request, say so politely.
    """; 

    [HttpGet] 
    public async IAsyncEnumerable<string> Get(
        [FromQuery] string message, 
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            yield return "Please enter a message. ";
            yield break; 
        }

        await using var mcpClient = await McpClientHelper.CreateAsync(
            configuration, 
            HttpContext, 
            cancellationToken
        ); 

        var availableTools = await mcpClient.ListToolsAsync(
            cancellationToken: cancellationToken
        ); 

        var productTools = availableTools
            .Where(tool => tool.Name is "get_products" or "get_single_product") 
            .ToList(); 

        var agent = chatClient.AsAIAgent(
            instructions: Instructions, 
            name: "CarvedRock Assistant", 
            tools: [.. productTools]
        ); 


        await foreach (var update in agent.RunStreamingAsync(
            message, 
            cancellationToken: cancellationToken
        ))
        {
            var text = update.ToString(); 
            if (!string.IsNullOrEmpty(text))
            {
                yield return text; 
            }
        }
    }
}
