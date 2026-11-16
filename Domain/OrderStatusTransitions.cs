using AttractiveCatalog.Api.Domain.Enums;

namespace AttractiveCatalog.Api.Domain;

public static class OrderStatusTransitions
{
    public static IReadOnlyList<OrderStatus> AllowedNextStatuses(OrderStatus status) => status switch
    {
        OrderStatus.New => [OrderStatus.Confirmed, OrderStatus.Cancelled],
        OrderStatus.Confirmed => [OrderStatus.Preparing, OrderStatus.Cancelled],
        OrderStatus.Preparing => [OrderStatus.OutForDelivery, OrderStatus.Cancelled],
        OrderStatus.OutForDelivery => [OrderStatus.Delivered, OrderStatus.Cancelled],
        _ => []
    };
}
