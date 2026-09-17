namespace AttractiveCatalog.Api.Domain.Entities;

public sealed class Brand : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? LogoUrl { get; set; }
    public string? LogoStorageKey { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
}