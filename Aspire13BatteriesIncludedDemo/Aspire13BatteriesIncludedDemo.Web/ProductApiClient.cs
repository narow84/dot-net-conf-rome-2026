namespace Aspire13BatteriesIncludedDemo.Web;

public class ProductApiClient(HttpClient httpClient)
{
    public async Task<Product[]> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<Product[]>("/products", cancellationToken) ?? [];
    }

    public async Task<Product?> GetProductAsync(int id, CancellationToken cancellationToken = default)
    {
        return await httpClient.GetFromJsonAsync<Product>($"/products/{id}", cancellationToken);
    }

    public async Task<Product?> CreateProductAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/products", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Product>(cancellationToken);
    }

    public async Task<bool> DeleteProductAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/products/{id}", cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

public record Product(int Id, string Name, string? Description, decimal Price, DateTime CreatedAt);

public record CreateProductRequest(string Name, string? Description, decimal Price);
