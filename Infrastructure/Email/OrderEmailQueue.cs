using System.Text.Json;
using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Infrastructure.Config;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace AttractiveCatalog.Api.Infrastructure.Email;

public sealed class OrderEmailQueue(AppDbContext dbContext, IOptions<OrderEmailOptions> options)
{
    public void Enqueue(Order order, string currency)
    {
        if (!options.Value.Enabled) return;

        var payload = JsonSerializer.Serialize(new OrderEmailPayload(
            order.Reference, order.CreatedAt, order.CustomerName, order.CustomerPhone, order.Total, currency,
            $"{options.Value.AdminBaseUrl.TrimEnd('/')}/admin/orders/{order.Id}",
            order.Items.Select(item => new OrderEmailItem(item.ProductName, item.Quantity, item.UnitPrice, item.LineTotal)).ToArray()));

        foreach (var recipient in options.Value.Recipients.Select(address => address.Trim().ToLowerInvariant()).Distinct())
        {
            dbContext.OrderEmailNotifications.Add(new OrderEmailNotification
            {
                OrderId = order.Id,
                Recipient = recipient,
                Payload = payload
            });
        }
    }
}

public sealed record OrderEmailPayload(string Reference, DateTime CreatedAt, string CustomerName, string CustomerPhone, decimal Total, string Currency, string AdminUrl, IReadOnlyList<OrderEmailItem> Items);
public sealed record OrderEmailItem(string Name, int Quantity, decimal UnitPrice, decimal LineTotal);
