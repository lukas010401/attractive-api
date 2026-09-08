using AttractiveCatalog.Api.Infrastructure.Config;
using Microsoft.Extensions.Options;

namespace AttractiveCatalog.Api.Infrastructure.Media;

public sealed class LocalProductImageStorage(IWebHostEnvironment environment, IOptions<MediaOptions> options) : IProductImageStorage
{
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

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var absolutePath = Path.Combine(absoluteFolder, fileName);
            await using (var stream = File.Create(absolutePath))
            {
                await file.CopyToAsync(stream, cancellationToken);
            }

            var storageKey = Path.Combine(relativeFolder, fileName).Replace('\\', '/');
            stored.Add(new StoredProductImage($"/{storageKey}", storageKey));
        }

        return stored;
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return Task.CompletedTask;

        var normalized = storageKey.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot"));
        var target = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "wwwroot", normalized));

        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) return Task.CompletedTask;
        if (File.Exists(target)) File.Delete(target);
        return Task.CompletedTask;
    }
}
