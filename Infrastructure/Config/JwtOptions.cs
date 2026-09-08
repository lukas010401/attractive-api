namespace AttractiveCatalog.Api.Infrastructure.Config;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = "AttractiveCatalog.Api";
    public string Audience { get; set; } = "AttractiveCatalog.Frontend";
    public string Key { get; set; } = "CHANGE_THIS_SUPER_SECRET_KEY_AT_LEAST_32_CHARS";
    public int ExpiryMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 15;
}
