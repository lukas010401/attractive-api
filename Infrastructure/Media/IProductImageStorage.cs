namespace AttractiveCatalog.Api.Infrastructure.Media;

public interface IProductImageStorage
{
    Task<IReadOnlyList<StoredProductImage>> SaveAsync(Guid productId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken);
    Task<StoredProductImageVariant?> GenerateCardVariantAsync(string storageKey, CancellationToken cancellationToken);
    Task<StoredBrandLogo> SaveBrandLogoAsync(Guid brandId, IFormFile file, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
    Task DeleteAsync(string? storageKey, string? cardStorageKey, CancellationToken cancellationToken);
}

public sealed record StoredProductImage(string Url, string StorageKey, string CardUrl, string CardStorageKey);
public sealed record StoredProductImageVariant(string Url, string StorageKey);
public sealed record StoredBrandLogo(string Url, string StorageKey);