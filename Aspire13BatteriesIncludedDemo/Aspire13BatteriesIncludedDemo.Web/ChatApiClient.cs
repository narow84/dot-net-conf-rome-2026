namespace Aspire13BatteriesIncludedDemo.Web;

public class ChatApiClient(HttpClient httpClient)
{
    public async Task<string> SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetFromJsonAsync<ChatResponse>($"/chat?message={Uri.EscapeDataString(message)}", cancellationToken);
        return response?.Reply ?? string.Empty;
    }
}

public record ChatResponse(string Reply);
