using AttractiveCatalog.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Infrastructure.Persistence;

public static class OrderStockQueries
{
    public static async Task<Order?> LoadOrderForUpdateAsync(AppDbContext dbContext, Guid id, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.Orders.SingleOrDefaultAsync(order => order.Id == id, cancellationToken);
        }

        return await dbContext.Orders
            .FromSqlInterpolated($"SELECT * FROM orders WHERE \"Id\" = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);
    }

    public static async Task<List<Product>> LoadProductsForUpdateAsync(AppDbContext dbContext, Guid[] productIds, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.Products.Where(product => productIds.Contains(product.Id)).OrderBy(product => product.Id).ToListAsync(cancellationToken);
        }

        return await dbContext.Products
            .FromSqlInterpolated($"SELECT * FROM products WHERE \"Id\" = ANY ({productIds}) ORDER BY \"Id\" FOR UPDATE")
            .ToListAsync(cancellationToken);
    }
}
