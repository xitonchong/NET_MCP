using CarvedRock.Core;
using CarvedRock.Data.Entities;
using CarvedRock.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;


[ApiController]
[Route("[controller]")]
public partial class ProductController(
    ILogger<ProductLogic> logger, 
    IProductLogic productLogic, 
    NewProductValidator validator): ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IEnumerable<Product>> Get(string category = "all")
    {
        return await productLogic.GetProductsForCategoryAsync(category); 
    }

    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Product), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(int id)
    {
        logger.LogDebug("Getting single product in API for {id}", id); 
        var product = await productLogic.GetProductIdAsync(id); 
        if (product != null)
        {
            return Ok(product); 
        }; 
        logger.LogWarning("No product found for ID: {id}", id); 
        return NotFound(); 
    }    
}