using Microsoft.EntityFrameworkCore;

namespace Aspire13BatteriesIncludedDemo.ApiService.Data;

/// <summary>
/// DbContext utilizzato per dimostrare il "connection shaping" di Aspire:
/// la stessa risorsa PostgreSQL "appDb" viene consumata sia come NpgsqlDataSource (raw ADO.NET)
/// sia come DbContext (EF Core). Aspire inietta la stessa connection string, ma ogni
/// client integration registra un servizio .NET diverso nel DI container.
/// </summary>
public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<ProductEntity> Products => Set<ProductEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Name).HasColumnName("name").IsRequired();
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Price).HasColumnName("price").HasColumnType("numeric(10,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });
    }
}

public class ProductEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public DateTime CreatedAt { get; set; }
}
