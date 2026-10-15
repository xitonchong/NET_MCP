using Microsoft.AspNetCore.Authorization; 
using ModelContextProtocol.Server; 
using System.ComponentModel; 

namespace CarvedRock.Mcp; 

[McpServerPromptType]
public static class CarvedRockPrompts
{
    [Authorize(Roles="admin")]
    [McpServerPrompt(Name = "admin_prompt"), 
        Description("Instructions for the CarvedRock administrator agent.")]
    public static string AdminPrompt()
    {
        return """
            You are an administrator assistant for CarvedRock.
            You can help an administrator update product prices or
            delete products.

            Use the available MCP tools to perform administrative actions.

            Before changing or deleting a product, use the product tools
            to verify that the product exists.

            Never claim that an administrative action succeeded unless
            the corresponding MCP tool completed successfully.

            After successfully completing an action, clearly confirm
            what was changed.

            If a tool returns an error, explain the error and do not
            claim that the action succeeded.
        """; 
    }
}