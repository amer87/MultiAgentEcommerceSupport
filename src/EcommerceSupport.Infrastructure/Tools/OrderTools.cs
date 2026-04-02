using System.ComponentModel;
using System.Text;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Infrastructure.Tools;

/// <summary>
/// Order-related tools exposed to the ShippingAgent and BillingAgent.
/// </summary>
public sealed class OrderTools(
    IOrderRepository orderRepository,
    ILogger<OrderTools> logger)
{
    [Description("Get the full details of a specific order by its order ID. " +
                 "Use this when the customer asks about a specific order status, " +
                 "items, or charges.")]
    public async Task<string> GetOrderDetailsAsync(
        [Description("The order ID, e.g. ORD-10001")] string orderId)
    {
        logger.LogInformation("GetOrderDetails called for {OrderId}", orderId);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null)
            return $"No order found with ID '{orderId}'. Please verify the order ID and try again.";

        var sb = new StringBuilder();
        sb.AppendLine($"Order ID: {order.OrderId}");
        sb.AppendLine($"Status: {order.Status}");
        sb.AppendLine($"Placed: {order.PlacedAt:yyyy-MM-dd HH:mm} UTC");
        sb.AppendLine($"Shipping to: {order.ShippingAddress}");
        sb.AppendLine($"Items:");
        foreach (var item in order.Items)
            sb.AppendLine($"  - {item.ProductName} × {item.Quantity} = {item.TotalPrice:C}");
        sb.AppendLine($"Subtotal: {order.SubTotal:C}");
        sb.AppendLine($"Shipping: {order.ShippingCost:C}");
        sb.AppendLine($"Total: {order.Total:C} {order.Currency}");

        if (order.Shipment is not null)
        {
            sb.AppendLine($"Carrier: {order.Shipment.Carrier}");
            sb.AppendLine($"Tracking: {order.Shipment.TrackingNumber}");
            sb.AppendLine($"Shipped: {order.Shipment.ShippedAt:yyyy-MM-dd}");
            sb.AppendLine($"Est. Delivery: {order.Shipment.EstimatedDelivery:yyyy-MM-dd}");
            sb.AppendLine($"Current Location: {order.Shipment.CurrentLocation}");
        }

        return sb.ToString();
    }

    [Description("Get the last 5 orders for a customer. Use this when the customer " +
                 "asks about their order history or most recent purchases.")]
    public async Task<string> GetRecentOrdersAsync(
        [Description("The customer ID, e.g. CUST-001")] string customerId)
    {
        logger.LogInformation("GetRecentOrders called for {CustomerId}", customerId);

        var orders = await orderRepository.GetByCustomerIdAsync(customerId, limit: 5);
        if (!orders.Any())
            return "No orders found for this customer.";

        var sb = new StringBuilder();
        sb.AppendLine($"Recent orders for customer {customerId}:");
        foreach (var o in orders)
            sb.AppendLine($"  • {o.OrderId} | {o.Status} | {o.Total:C} | {o.PlacedAt:yyyy-MM-dd}");

        return sb.ToString();
    }

    [Description("Track the shipment for a specific order. Returns carrier, tracking number, " +
                 "and current location/status.")]
    public async Task<string> TrackShipmentAsync(
        [Description("The order ID to track")] string orderId)
    {
        logger.LogInformation("TrackShipment called for {OrderId}", orderId);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null)
            return $"Order '{orderId}' not found.";

        if (order.Shipment is null)
            return $"Order '{orderId}' has not been shipped yet. Current status: {order.Status}.";

        return $"Tracking for {orderId}: Carrier={order.Shipment.Carrier}, " +
               $"Tracking#={order.Shipment.TrackingNumber}, " +
               $"Status={order.Status}, " +
               $"Location={order.Shipment.CurrentLocation}, " +
               $"Est. Delivery={order.Shipment.EstimatedDelivery:yyyy-MM-dd}.";
    }
}
