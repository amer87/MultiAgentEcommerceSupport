namespace EcommerceSupport.Core.Models;

public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    OutForDelivery,
    Delivered,
    Cancelled,
    Refunded,
    ReturnRequested,
    Returned
}

public record OrderItem(
    string ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice
)
{
    public decimal TotalPrice => Quantity * UnitPrice;
}

public record ShipmentInfo(
    string Carrier,
    string TrackingNumber,
    DateTime? ShippedAt,
    DateTime? EstimatedDelivery,
    string? CurrentLocation
);

public class Order
{
    public string OrderId { get; init; } = default!;
    public string CustomerId { get; init; } = default!;
    public OrderStatus Status { get; set; }
    public DateTime PlacedAt { get; init; }
    public DateTime? UpdatedAt { get; set; }
    public IReadOnlyList<OrderItem> Items { get; init; } = [];
    public decimal SubTotal => Items.Sum(i => i.TotalPrice);
    public decimal ShippingCost { get; init; }
    public decimal Total => SubTotal + ShippingCost;
    public string Currency { get; init; } = "USD";
    public ShipmentInfo? Shipment { get; set; }
    public string? CancellationReason { get; set; }
    public string ShippingAddress { get; init; } = default!;
}
