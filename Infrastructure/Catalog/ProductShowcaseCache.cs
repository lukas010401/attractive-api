namespace AttractiveCatalog.Api.Infrastructure.Catalog;

public sealed class ProductShowcaseCache : IDisposable
{
    private readonly SemaphoreSlim refreshLock = new(1, 1);
    private readonly object stateLock = new();
    private IReadOnlyList<ProductShowcaseItem>? products;
    private DateTimeOffset expiresAt;
    private long revision;

    public async Task<IReadOnlyList<ProductShowcaseItem>> GetAsync(
        Func<CancellationToken, Task<IReadOnlyList<ProductShowcaseItem>>> loadProducts,
        CancellationToken cancellationToken)
    {
        lock (stateLock)
        {
            if (products is not null && DateTimeOffset.UtcNow < expiresAt) return products;
        }

        await refreshLock.WaitAsync(cancellationToken);
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                long currentRevision;
                lock (stateLock)
                {
                    if (products is not null && DateTimeOffset.UtcNow < expiresAt) return products;
                    currentRevision = revision;
                }

                var loadedProducts = await loadProducts(cancellationToken);
                lock (stateLock)
                {
                    if (currentRevision != revision) continue;
                    products = loadedProducts;
                    expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
                    return products;
                }
            }
        }
        finally
        {
            refreshLock.Release();
        }
    }

    public void Invalidate()
    {
        lock (stateLock)
        {
            products = null;
            revision++;
        }
    }

    public void Dispose() => refreshLock.Dispose();
}

public sealed record ProductShowcaseItem(Guid Id, string Name, string Slug, string? BrandName, string PrimaryImageUrl);
