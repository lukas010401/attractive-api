namespace AttractiveCatalog.Api.Domain.Entities;

public sealed class ProductSpecification : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public Product? Product { get; set; }
}
