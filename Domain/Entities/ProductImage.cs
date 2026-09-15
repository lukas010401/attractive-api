namespace AttractiveCatalog.Api.Domain.Entities;

public sealed class ProductImage : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public string? CardUrl { get; set; }
    public string? CardStorageKey { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsPrimary { get; set; }
    public Product? Product { get; set; }
}
