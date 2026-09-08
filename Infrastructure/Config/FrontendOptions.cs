namespace AttractiveCatalog.Api.Infrastructure.Config;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";
    public string[] AllowedOrigins { get; set; } = [];
}
