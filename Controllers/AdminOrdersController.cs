using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using AttractiveCatalog.Api.Domain;
using AttractiveCatalog.Api.Infrastructure.Catalog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(AppDbContext dbContext, ProductShowcaseCache showcaseCache) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 8, CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = dbContext.Orders.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<OrderStatus>(status, true, out var parsedStatus)) query = query.Where(x => x.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToLower();
            query = query.Where(x =>
                x.Reference.ToLower().Contains(normalizedSearch)
                || x.CustomerName.ToLower().Contains(normalizedSearch)
                || x.CustomerPhone.ToLower().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query.OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new AdminOrderListItem(x.Id, x.Reference, x.CustomerName, x.CustomerPhone, x.Total, x.Status.ToString(), x.PaymentMethod.ToString(), x.Items.Count, x.CreatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResponse<AdminOrderListItem>(orders, totalCount, page, pageSize));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken cancellationToken)
    {
        var order = await BuildOrderDetailAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateOrderStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status) || !Enum.IsDefined(status))
        {
            return BadRequest(new { message = "Le statut de commande est invalide." });
        }

        await using var transaction = dbContext.Database.IsRelational()
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var order = await OrderStockQueries.LoadOrderForUpdateAsync(dbContext, id, cancellationToken);
        if (order is null) return NotFound();

        var statusChanged = order.Status != status;
        if (statusChanged)
        {
            if (request.ExpectedStatus is not null
                && (!Enum.TryParse<OrderStatus>(request.ExpectedStatus, true, out var expectedStatus) || expectedStatus != order.Status))
            {
                return Conflict(new { message = "Le statut a été modifié entre-temps. La commande doit être actualisée avant de continuer." });
            }

            if (!OrderStatusTransitions.AllowedNextStatuses(order.Status).Contains(status))
            {
                return Conflict(new { message = "Ce changement de statut n’est plus autorisé pour cette commande." });
            }

            if (status == OrderStatus.Cancelled)
            {
                if (order.Status == OrderStatus.OutForDelivery && !request.ItemsReturned)
                {
                    return BadRequest(new { message = "Confirmez le retour de tous les articles avant d’annuler une commande en livraison." });
                }

                var items = await dbContext.OrderItems.AsNoTracking()
                    .Where(item => item.OrderId == id)
                    .GroupBy(item => item.ProductId)
                    .Select(group => new { ProductId = group.Key, Quantity = group.Sum(item => item.Quantity) })
                    .ToListAsync(cancellationToken);

                var products = await OrderStockQueries.LoadProductsForUpdateAsync(dbContext, items.Select(item => item.ProductId).ToArray(), cancellationToken);
                if (products.Count != items.Count || items.Any(item => item.Quantity <= 0))
                {
                    return Conflict(new { message = "Les articles de la commande ne permettent pas une remise en stock automatique. Vérifiez la commande." });
                }

                var quantities = items.ToDictionary(item => item.ProductId, item => item.Quantity);
                foreach (var product in products)
                {
                    product.StockQuantity = checked(product.StockQuantity + quantities[product.Id]);
                    if (product.Status == ProductStatus.OutOfStock && product.StockQuantity > 0)
                    {
                        product.Status = ProductStatus.Published;
                    }
                }
            }

            order.Status = status;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        if (statusChanged && status == OrderStatus.Cancelled) showcaseCache.Invalidate();

        var detail = await BuildOrderDetailAsync(id, cancellationToken);
        return detail is null ? NotFound() : Ok(detail);
    }

    private async Task<AdminOrderDetail?> BuildOrderDetailAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id,
                x.Reference,
                x.CustomerName,
                x.CustomerPhone,
                x.DeliveryAddress,
                x.CustomerNote,
                x.Total,
                x.Status,
                PaymentMethod = x.PaymentMethod.ToString(),
                x.CreatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null) return null;

        var items = await dbContext.OrderItems.AsNoTracking()
            .Where(x => x.OrderId == id)
            .OrderBy(x => x.CreatedAt)
            .Select(x => new AdminOrderItemDetail(x.Id, x.ProductId, x.ProductName, x.ProductSku, x.UnitPrice, x.Quantity, x.LineTotal))
            .ToListAsync(cancellationToken);

        return new AdminOrderDetail(
            order.Id,
            order.Reference,
            order.CustomerName,
            order.CustomerPhone,
            order.DeliveryAddress,
            order.CustomerNote,
            order.Total,
            order.Status.ToString(),
            order.PaymentMethod,
            items.Count,
            order.CreatedAt,
            items,
            OrderStatusTransitions.AllowedNextStatuses(order.Status).Select(status => status.ToString()).ToArray());
    }
}

public sealed record AdminOrderListItem(Guid Id, string Reference, string CustomerName, string CustomerPhone, decimal Total, string Status, string PaymentMethod, int ItemCount, DateTime CreatedAt);
public sealed record AdminOrderDetail(Guid Id, string Reference, string CustomerName, string CustomerPhone, string DeliveryAddress, string? CustomerNote, decimal Total, string Status, string PaymentMethod, int ItemCount, DateTime CreatedAt, IReadOnlyList<AdminOrderItemDetail> Items, IReadOnlyList<string> AllowedStatuses);
public sealed record AdminOrderItemDetail(Guid Id, Guid ProductId, string ProductName, string? ProductSku, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record UpdateOrderStatusRequest(string Status, string? ExpectedStatus = null, bool ItemsReturned = false);
