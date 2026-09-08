namespace AttractiveCatalog.Api.Infrastructure.Config;

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public int MaxImagesPerProduct { get; set; } = 10;
    public int MaxImageSizeMb { get; set; } = 5;
    public string[] AllowedExtensions { get; set; } = [".jpg", ".jpeg", ".png", ".webp"];
    public string UploadsRoot { get; set; } = "wwwroot/uploads";
}
