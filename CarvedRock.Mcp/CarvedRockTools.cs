using ModelContextProtocol.Server; 
using System.ComponentModel;

namespace CarvedRock.Mcp; 


[McpServerToolType]
public class CarvedRockTools(IHttpClientFactory httpClientFactory, 
    ILogger<CarvedRockTools> logger
)
{
    [McpServerTool(Name = "get_products"), 
     Description("Get a list of all available products")]
    public async Task<List<ProductModel>> GetAllProductAsync(
        CancellationToken cancellationToken = default
    )
    {
        var client = httpClientFactory.CreateClient("CarvedRockApi");
        var response = await client.GetFromJsonAsync<List<ProductModel>>("/product", cancellationToken); 

        logger.LogInformation("Fetched {Count} products", response?.Count ?? 0); 
        return response ?? []; 
    }


    [McpServerTool(Name="get_single_product"), Description("Get a single product based on Id. ")]
    public async Task<ProductModel?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("CarvedRockApi"); 
        return await client.GetFromJsonAsync<ProductModel>($"/product/{id}", cancellationToken); 
    }
}