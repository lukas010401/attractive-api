using AttractiveCatalog.Api.Domain.Entities;
using AttractiveCatalog.Api.Domain.Enums;
using AttractiveCatalog.Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace AttractiveCatalog.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed partial class PublicOrdersController(AppDbContext dbContext) : ControllerBase
{
    private const int MaxCustomerNameLength = 150;
    private const int MaxCustomerPhoneLength = 30;
    private const int MaxDeliveryAddressLength = 500;
    private const int MaxCustomerNoteLength = 1000;
    private const int MaxDistinctItems = 30;
    private const int MaxQuantityPerItem = 99;

    [HttpPost]
    [EnableRateLimiting("PublicOrders")]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var validationError = ValidateRequest(request);
        if (validationError is not null) return BadRequest(new { message = validationError });

        var requestedItems = request.Items
            .GroupBy(x => x.ProductId)
            .Select(group => new CreateOrderItemRequest(group.Key, group.Sum(x => x.Quantity)))
            .ToList();

        if (requestedItems.Any(x => x.Quantity > MaxQuantityPerItem)) return BadRequest(new { message = $"La quantité maximale par produit est {MaxQuantityPerItem}." });

        var productIds = requestedItems.Select(x => x.ProductId).ToList();
        var products = await dbContext.Products
            .Where(x => productIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (products.Count != productIds.Count) return BadRequest(new { message = "Un ou plusieurs produits sont introuvables." });

        var unavailableProduct = products.FirstOrDefault(x => x.Status != ProductStatus.Published || x.StockQuantity <= 0);
        if (unavailableProduct is not null) return BadRequest(new { message = $"Le produit {unavailableProduct.Name} n'est plus disponible." });

        foreach (var item in requestedItems)
        {
            var product = products.Single(x => x.Id == item.ProductId);
            if (product.StockQuantity < item.Quantity) return BadRequest(new { message = $"Stock insuffisant pour {product.Name}. Stock disponible : {product.StockQuantity}." });
        }

        var order = new Order
        {
            Reference = await BuildUniqueReferenceAsync(cancellationToken),
            CustomerName = request.CustomerName.Trim(),
            CustomerPhone = request.CustomerPhone.Trim(),
            DeliveryAddress = request.DeliveryAddress.Trim(),
            CustomerNote = string.IsNullOrWhiteSpace(request.CustomerNote) ? null : request.CustomerNote.Trim(),
            Status = OrderStatus.New,
            PaymentMethod = PaymentMethod.CashOnDelivery,
            DeliveryFee = 0
        };

        foreach (var item in requestedItems)
        {
            var product = products.Single(x => x.Id == item.ProductId);
            var lineTotal = product.Price * item.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                ProductSku = product.Sku,
                UnitPrice = product.Price,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
            product.StockQuantity -= item.Quantity;
            if (product.StockQuantity == 0) product.Status = ProductStatus.OutOfStock;
        }

        order.Subtotal = order.Items.Sum(x => x.LineTotal);
        order.Total = order.Subtotal + order.DeliveryFee;

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new { order.Id, order.Reference, Status = order.Status.ToString(), PaymentMethod = order.PaymentMethod.ToString(), order.Total });
    }

    private static string? ValidateRequest(CreateOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerName)) return "Le nom est obligatoire.";
        if (request.CustomerName.Trim().Length > MaxCustomerNameLength) return $"Le nom ne doit pas dépasser {MaxCustomerNameLength} caractères.";
        if (string.IsNullOrWhiteSpace(request.CustomerPhone)) return "Le téléphone est obligatoire.";
        if (request.CustomerPhone.Trim().Length > MaxCustomerPhoneLength) return $"Le téléphone ne doit pas dépasser {MaxCustomerPhoneLength} caractères.";
        if (!PhoneRegex().IsMatch(request.CustomerPhone.Trim())) return "Le téléphone n'est pas valide.";
        if (string.IsNullOrWhiteSpace(request.DeliveryAddress)) return "L'adresse de livraison est obligatoire.";
        if (request.DeliveryAddress.Trim().Length > MaxDeliveryAddressLength) return $"L'adresse ne doit pas dépasser {MaxDeliveryAddressLength} caractères.";
        if (!string.IsNullOrWhiteSpace(request.CustomerNote) && request.CustomerNote.Trim().Length > MaxCustomerNoteLength) return $"La note ne doit pas dépasser {MaxCustomerNoteLength} caractères.";
        if (request.Items.Count == 0) return "Le panier est vide.";
        if (request.Items.Count > MaxDistinctItems) return $"Le panier ne peut pas contenir plus de {MaxDistinctItems} lignes.";
        if (request.Items.Any(x => x.ProductId == Guid.Empty)) return "Un produit du panier est invalide.";
        if (request.Items.Any(x => x.Quantity <= 0)) return "La quantité doit être supérieure à zéro.";
        if (request.Items.Sum(x => x.Quantity) > MaxDistinctItems * MaxQuantityPerItem) return "Le panier contient trop d'articles.";
        return null;
    }

    private async Task<string> BuildUniqueReferenceAsync(CancellationToken cancellationToken)
    {
        string reference;
        do
        {
            reference = $"ATT-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        } while (await dbContext.Orders.AnyAsync(x => x.Reference == reference, cancellationToken));

        return reference;
    }

    [GeneratedRegex(@"^[0-9+() .-]{6,30}$")]
    private static partial Regex PhoneRegex();
}

public sealed record CreateOrderRequest(string CustomerName, string CustomerPhone, string DeliveryAddress, string? CustomerNote, decimal DeliveryFee, IReadOnlyList<CreateOrderItemRequest> Items);
public sealed record CreateOrderItemRequest(Guid ProductId, int Quantity);



