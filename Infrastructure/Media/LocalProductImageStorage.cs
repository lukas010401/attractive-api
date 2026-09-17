using AttractiveCatalog.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace AttractiveCatalog.Api.Infrastructure.Media;

public sealed class LocalProductImageStorage(IWebHostEnvironment environment, IOptions<MediaOptions> options) : IProductImageStorage
{
    private const int CardMaxWidth = 900;
    private const int CardMaxHeight = 1200;
    private const int CardQuality = 88;
    private readonly MediaOptions _options = options.Value;

    public async Task<IReadOnlyList<StoredProductImage>> SaveAsync(Guid productId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken)
    {
        if (files.Count == 0) return [];
        if (files.Count > _options.MaxImagesPerProduct) throw new InvalidOperationException($"Maximum {_options.MaxImagesPerProduct} images allowed.");

        var allowedExtensions = new HashSet<string>(_options.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
        var maxBytes = _options.MaxImageSizeMb * 1024L * 1024L;
        var relativeFolder = Path.Combine("uploads", "products", productId.ToString("N"));
        var absoluteFolder = Path.Combine(environment.ContentRootPath, "wwwroot", relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var stored = new List<StoredProductImage>();
        foreach (var file in files)
        {
            if (file.Length <= 0) continue;
            if (file.Length > maxBytes) throw new InvalidOperationException($"Image {file.FileName} exceeds {_options.MaxImageSizeMb} MB.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension)) throw new InvalidOperationException($"Unsupported image format: {extension}.");

            var imageId = Guid.NewGuid().ToString("N");
            var originalFileName = $"{imageId}{extension}";
            var originalAbsolutePath = Path.Combine(absoluteFolder, originalFileName);

            await using (var stream = File.Create(originalAbsolutePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var originalStorageKey = Path.Combine(relativeFolder, originalFileName).Replace('\\', '/');
            var card = await GenerateCardVariantAsync(originalStorageKey, cancellationToken)
                ?? throw new InvalidOperationException($"Unable to generate optimized card image for {file.FileName}.");

            stored.Add(new StoredProductImage($"/{originalStorageKey}", originalStorageKey, card.Url, card.StorageKey));
        }

        return stored;
    }

    public async Task<StoredBrandLogo> SaveBrandLogoAsync(Guid brandId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new InvalidOperationException("Logo file is empty.");

        var allowedExtensions = new HashSet<string>(_options.AllowedExtensions, StringComparer.OrdinalIgnoreCase);
        var maxBytes = _options.MaxImageSizeMb * 1024L * 1024L;
        if (file.Length > maxBytes) throw new InvalidOperationException($"Logo {file.FileName} exceeds {_options.MaxImageSizeMb} MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(extension)) throw new InvalidOperationException($"Unsupported image format: {extension}.");

        var relativeFolder = Path.Combine("uploads", "brands", brandId.ToString("N"));
        var absoluteFolder = Path.Combine(environment.ContentRootPath, "wwwroot", relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteFolder, fileName);
        await using (var stream = File.Create(absolutePath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var storageKey = Path.Combine(relativeFolder, fileName).Replace('\\', '/');
        return new StoredBrandLogo($"/{storageKey}", storageKey);
    }

    public async Task<StoredProductImageVariant?> GenerateCardVariantAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return null;

        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        var originalPath = Path.GetFullPath(Path.Combine(root, normalized));
        if (!originalPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(originalPath)) return null;

        var directory = Path.GetDirectoryName(originalPath);
        if (string.IsNullOrWhiteSpace(directory)) return null;

        var cardFileName = $"{Path.GetFileNameWithoutExtension(originalPath)}-card.webp";
        var cardPath = Path.Combine(directory, cardFileName);

        if (!File.Exists(cardPath))
        {
            using var image = await Image.LoadAsync(originalPath, cancellationToken);
            image.Mutate(context => context.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(CardMaxWidth, CardMaxHeight)
            }));

            await image.SaveAsWebpAsync(cardPath, new WebpEncoder { Quality = CardQuality }, cancellationToken);
        }

        var relativeCardKey = Path.GetRelativePath(root, cardPath).Replace('\\', '/');
        return new StoredProductImageVariant($"/{relativeCardKey}", relativeCardKey);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        DeleteFile(storageKey);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string? storageKey, string? cardStorageKey, CancellationToken cancellationToken)
    {
        DeleteFile(storageKey);
        DeleteFile(cardStorageKey);
        return Task.CompletedTask;
    }

    private void DeleteFile(string? storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;

        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        var target = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot", normalized));

        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return;
        if (File.Exists(target)) File.Delete(target);
    }
}
