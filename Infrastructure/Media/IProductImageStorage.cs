namespace AttractiveCatalog.Api.Infrastructure.Media;

public interface IProductImageStorage
{
    Task<IReadOnlyList<StoredProductImage>> SaveAsync(Guid productId, IReadOnlyList<IFormFile> files, CancellationToken cancellationToken);
    Task DeleteAsync(string storageKey, CancellationToken cancellationToken);
}

public sealed record StoredProductImage(string Url, string StorageKey);
