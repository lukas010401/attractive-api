using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Authorize(Policy = "AdminPolicy")]
[Route("api/admin/orders")]
public sealed class AdminOrdersController(AppDbContext dbContext) : ControllerBase
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
        if (!Enum.TryParse<OrderStatus>(request.Status, true, out var status)) return BadRequest("Invalid order status.");

        var order = await dbContext.Orders.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (order is null) return NotFound();

        order.Status = status;
        await dbContext.SaveChangesAsync(cancellationToken);

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
                Status = x.Status.ToString(),
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
            order.Status,
            order.PaymentMethod,
            items.Count,
            order.CreatedAt,
            items);
    }
}

public sealed record AdminOrderListItem(Guid Id, string Reference, string CustomerName, string CustomerPhone, decimal Total, string Status, string PaymentMethod, int ItemCount, DateTime CreatedAt);
public sealed record AdminOrderDetail(Guid Id, string Reference, string CustomerName, string CustomerPhone, string DeliveryAddress, string? CustomerNote, decimal Total, string Status, string PaymentMethod, int ItemCount, DateTime CreatedAt, IReadOnlyList<AdminOrderItemDetail> Items);
public sealed record AdminOrderItemDetail(Guid Id, Guid ProductId, string ProductName, string? ProductSku, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record UpdateOrderStatusRequest(string Status);
