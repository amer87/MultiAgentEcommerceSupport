using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Infrastructure.Repositories;

// TODO : Change to real database
public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly Dictionary<string, Order> _orders;

    public InMemoryOrderRepository()
    {
        _orders = SeedData().ToDictionary(o => o.OrderId);
    }

    public Task<Order?> GetByIdAsync(string orderId, CancellationToken ct = default)
        => Task.FromResult(_orders.GetValueOrDefault(orderId.ToUpper()));

    public Task<IReadOnlyList<Order>> GetByCustomerIdAsync(
        string customerId, int limit = 10, CancellationToken ct = default)
    {
        IReadOnlyList<Order> result = [.. _orders.Values
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.PlacedAt)
            .Take(limit)];
        return Task.FromResult(result);
    }

    public Task<Order?> GetLatestOrderAsync(string customerId, CancellationToken ct = default)
    {
        var order = _orders.Values
            .Where(o => o.CustomerId == customerId)
            .MaxBy(o => o.PlacedAt);
        return Task.FromResult(order);
    }

    public Task UpdateStatusAsync(string orderId, OrderStatus status,
        string? reason = null, CancellationToken ct = default)
    {
        if (_orders.TryGetValue(orderId.ToUpper(), out var order))
        {
            order.Status = status;
            order.UpdatedAt = DateTime.UtcNow;
            if (reason != null) order.CancellationReason = reason;
        }
        return Task.CompletedTask;
    }

    private static IEnumerable<Order> SeedData()
    {
        var now = DateTime.UtcNow;

        yield return new Order
        {
            OrderId = "ORD-10001",
            CustomerId = "CUST-001",
            Status = OrderStatus.Shipped,
            PlacedAt = now.AddDays(-3),
            ShippingAddress = "111 Main St, B89 1QQ, London, UK",
            ShippingCost = 9.99m,
            Items =
            [
                new("PROD-A1", "Wireless Headphones Pro X", 1, 149.99m),
                new("PROD-B2", "USB-C Charging Cable 2m", 2, 12.99m)
            ],
            Shipment = new ShipmentInfo(
                "FedEx",
                "TRK-00123456789",
                now.AddDays(-2),
                now.AddDays(1),
                "Seattle Distribution Center")
        };

        yield return new Order
        {
            OrderId = "ORD-10002",
            CustomerId = "CUST-001",
            Status = OrderStatus.Delivered,
            PlacedAt = now.AddDays(-15),
            ShippingAddress = "111 Main St, B89 1QQ, London, UK",
            ShippingCost = 0m,
            Items =
            [
                new("PROD-C3", "Mechanical Keyboard TKL", 1, 89.99m)
            ],
            Shipment = new ShipmentInfo(
                "UPS",
                "TRK-00987654321",
                now.AddDays(-14),
                now.AddDays(-12),
                "Delivered")
        };

        yield return new Order
        {
            OrderId = "ORD-10003",
            CustomerId = "CUST-002",
            Status = OrderStatus.Processing,
            PlacedAt = now.AddHours(-6),
            ShippingAddress = "222 Main St, B89 1QQ, London, UK",
            ShippingCost = 4.99m,
            Items =
            [
                new("PROD-D4", "Smart Watch Ultra", 1, 299.99m),
                new("PROD-E5", "Watch Protective Case", 1, 19.99m)
            ]
        };

        yield return new Order
        {
            OrderId = "ORD-10004",
            CustomerId = "CUST-003",
            Status = OrderStatus.Delivered,
            PlacedAt = now.AddDays(-30),
            ShippingAddress = "333 Main St, B89 1QQ, London, UK",
            ShippingCost = 0m,
            Items =
            [
                new("PROD-F6", "4K Monitor 27-inch", 1, 449.99m),
            ],
            Shipment = new ShipmentInfo(
                "DHL",
                "TRK-0056781234",
                now.AddDays(-28),
                now.AddDays(-26),
                "Delivered")
        };
    }
}
