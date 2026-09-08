using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/dashboard")]
public sealed class AdminDashboardController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var todayStart = DateTime.UtcNow.Date;
        var tomorrowStart = todayStart.AddDays(1);

        var orderRows = await dbContext.Orders.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new StatusAmountRow(x.Key, x.Count(), x.Sum(order => order.Total)))
            .ToListAsync(cancellationToken);

        var productRows = await dbContext.Products.AsNoTracking()
            .GroupBy(x => x.Status)
            .Select(x => new StatusCountRow(x.Key, x.Count()))
            .ToListAsync(cancellationToken);

        var todayOrders = await dbContext.Orders.AsNoTracking()
            .Where(x => x.CreatedAt >= todayStart && x.CreatedAt < tomorrowStart)
            .GroupBy(_ => 1)
            .Select(x => new CountAmount(x.Count(), x.Sum(order => order.Total)))
            .SingleOrDefaultAsync(cancellationToken) ?? new CountAmount(0, 0);

        var lowStockCount = await dbContext.Products.AsNoTracking()
            .CountAsync(x => x.StockQuantity > 0 && x.StockQuantity <= 2, cancellationToken);

        var featuredCount = await dbContext.Products.AsNoTracking()
            .CountAsync(x => x.IsFeatured, cancellationToken);

        var recentOrders = await dbContext.Orders.AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .Select(x => new DashboardRecentOrder(
                x.Id,
                x.Reference,
                x.CustomerName,
                x.CustomerPhone,
                x.Total,
                x.Status.ToString(),
                x.Items.Count,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        var orders = new DashboardOrdersSummary(
            GetCount(orderRows, OrderStatus.New) + GetCount(orderRows, OrderStatus.Confirmed),
            GetCount(orderRows, OrderStatus.Preparing),
            GetCount(orderRows, OrderStatus.OutForDelivery),
            GetAmount(orderRows, OrderStatus.OutForDelivery),
            GetCount(orderRows, OrderStatus.Delivered),
            GetAmount(orderRows, OrderStatus.Delivered),
            GetCount(orderRows, OrderStatus.Cancelled),
            GetAmount(orderRows, OrderStatus.Cancelled),
            todayOrders.Count,
            todayOrders.Amount,
            orderRows.Sum(x => x.Count),
            orderRows.Sum(x => x.Amount));

        var products = new DashboardProductsSummary(
            productRows.Sum(x => x.Count),
            GetCount(productRows, ProductStatus.Published),
            GetCount(productRows, ProductStatus.Draft),
            GetCount(productRows, ProductStatus.Hidden),
            GetCount(productRows, ProductStatus.OutOfStock),
            lowStockCount,
            featuredCount);

        return Ok(new AdminDashboardResponse(orders, products, recentOrders));
    }

    private static int GetCount(IReadOnlyList<StatusAmountRow> rows, OrderStatus status)
    {
        return rows.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
    }

    private static decimal GetAmount(IReadOnlyList<StatusAmountRow> rows, OrderStatus status)
    {
        return rows.FirstOrDefault(x => x.Status == status)?.Amount ?? 0;
    }

    private static int GetCount(IReadOnlyList<StatusCountRow> rows, ProductStatus status)
    {
        return rows.FirstOrDefault(x => x.Status == status)?.Count ?? 0;
    }
}

public sealed record AdminDashboardResponse(DashboardOrdersSummary Orders, DashboardProductsSummary Products, IReadOnlyList<DashboardRecentOrder> RecentOrders);
public sealed record DashboardOrdersSummary(int PendingCount, int PreparingCount, int OutForDeliveryCount, decimal OutForDeliveryTotal, int DeliveredCount, decimal DeliveredTotal, int CancelledCount, decimal CancelledTotal, int TodayCount, decimal TodayTotal, int TotalCount, decimal TotalAmount);
public sealed record DashboardProductsSummary(int TotalCount, int PublishedCount, int DraftCount, int HiddenCount, int OutOfStockCount, int LowStockCount, int FeaturedCount);
public sealed record DashboardRecentOrder(Guid Id, string Reference, string CustomerName, string CustomerPhone, decimal Total, string Status, int ItemCount, DateTime CreatedAt);
public sealed record StatusAmountRow(OrderStatus Status, int Count, decimal Amount);
public sealed record StatusCountRow(ProductStatus Status, int Count);
public sealed record CountAmount(int Count, decimal Amount);

