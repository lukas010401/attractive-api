namespace AttractiveCatalog.Api.Infrastructure.Config;

public sealed class OrderEmailOptions
{
    public const string SectionName = "OrderEmail";
    public bool Enabled { get; set; }
    public string Host { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Attractive Cosmetics";
    public string[] Recipients { get; set; } = [];
    public string AdminBaseUrl { get; set; } = "https://attractive-store.com";
    public int PollIntervalSeconds { get; set; } = 10;
    public int BatchSize { get; set; } = 10;
    public int SendTimeoutSeconds { get; set; } = 30;
}
