namespace AttractiveCatalog.Api.Domain.Entities;

public sealed class SiteVisit : BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionKey { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
}
