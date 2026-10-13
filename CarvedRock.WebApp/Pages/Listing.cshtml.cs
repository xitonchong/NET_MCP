using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CarvedRock.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CarvedRock.WebApp.Pages;

public sealed record ChatTurn(string Role, string Content);

public class ListingModel(IProductService products, IHttpClientFactory httpClientFactory) : PageModel
{
    private const string ChatHistoryKey = "agent-chat-history";
    public List<ProductModel> Products { get; private set; } = [];
    public string CategoryName { get; private set; } = "all";
    public IReadOnlyList<ChatTurn> ChatHistory { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string cat = "all")
    {
        if (cat != "all" && !Constants.Categories.Contains(cat)) return BadRequest();
        CategoryName = cat;
        Products = await products.GetProductsAsync(cat);
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);
        ChatHistory = ReadChatHistory();
        return Page();
    }

    public async Task<IActionResult> OnGetChatAsync(string message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message)) return BadRequest("A message is required.");
        var accessToken = await HttpContext.GetTokenAsync("access_token");
        if (string.IsNullOrWhiteSpace(accessToken)) return Challenge();

        await HttpContext.Session.LoadAsync(cancellationToken);
        var history = ReadChatHistory();
        HttpContext.Session.SetString(ChatHistoryKey, JsonSerializer.Serialize(history));

        var client = httpClientFactory.CreateClient("AI");
        using var request = new HttpRequestMessage(HttpMethod.Post, "Agent/chat")
        {
            Content = JsonContent.Create(new { Message = message, History = history })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var apiResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        apiResponse.EnsureSuccessStatusCode();

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Append("X-Accel-Buffering", "no");
        await using var responseStream = await apiResponse.Content.ReadAsStreamAsync(cancellationToken);
        var assistantResponse = new StringBuilder();
        await foreach (var text in JsonSerializer.DeserializeAsyncEnumerable<string>(responseStream, cancellationToken: cancellationToken))
        {
            if (string.IsNullOrEmpty(text)) continue;
            assistantResponse.Append(text);
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(text)}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        if (assistantResponse.Length > 0)
        {
            history.Add(new ChatTurn("user", message));
            history.Add(new ChatTurn("assistant", assistantResponse.ToString()));
            HttpContext.Session.SetString(ChatHistoryKey, JsonSerializer.Serialize(history.TakeLast(10).ToList()));
            await HttpContext.Session.CommitAsync(cancellationToken);
        }
        await Response.WriteAsync("event: end\ndata: done\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
        return new EmptyResult();
    }

    private List<ChatTurn> ReadChatHistory()
    {
        var serialized = HttpContext.Session.GetString(ChatHistoryKey);
        if (string.IsNullOrWhiteSpace(serialized)) return [];
        try { return JsonSerializer.Deserialize<List<ChatTurn>>(serialized) ?? []; }
        catch (JsonException) { return []; }
    }
}
