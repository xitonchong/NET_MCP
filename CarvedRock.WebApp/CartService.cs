using System.Text.Json;
namespace CarvedRock.WebApp;

public record CartItem(int Id, int Quantity, string Name, double Price)
{
    public double Total => Quantity * Price;
}

public sealed class CartService(IHttpContextAccessor accessor, IProductService products)
{
    private ISession Session => accessor.HttpContext!.Session;
    private Dictionary<int, int> Quantities => JsonSerializer.Deserialize<Dictionary<int, int>>(Session.GetString("cart") ?? "{}") ?? [];
    public async Task<List<CartItem>> GetItemsAsync()
    {
        var quantities = Quantities;
        var available = await products.GetProductsAsync();
        return available.Where(p => quantities.TryGetValue(p.Id, out var quantity) && quantity is > 0 and <= 99)
            .Select(p => new CartItem(p.Id, quantities[p.Id], p.Name, p.Price)).ToList();
    }
    public async Task AddAsync(int id)
    {
        if (await products.GetProductByIdAsync(id) is null) throw new KeyNotFoundException();
        var quantities = Quantities;
        quantities[id] = Math.Min(quantities.GetValueOrDefault(id) + 1, 99);
        Session.SetString("cart", JsonSerializer.Serialize(quantities));
    }
    public void Remove(int id)
    {
        var quantities = Quantities;
        quantities.Remove(id);
        Session.SetString("cart", JsonSerializer.Serialize(quantities));
    }
    public void Clear() => Session.Remove("cart");
}
