using System.ComponentModel;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Infrastructure.Tools;

/// <summary>
/// Shipping-specific tools exposed to the ShippingAgent.
/// </summary>
public sealed class ShippingTools(
    IOrderRepository orderRepository,
    ILogger<ShippingTools> logger)
{
    [Description("Get real-time shipment tracking information for an order, " +
                 "including carrier, tracking number, current location and expected delivery.")]
    public async Task<string> GetTrackingInfoAsync(
        [Description("The order ID to track")] string orderId)
    {
        logger.LogInformation("GetTrackingInfo for {OrderId}", orderId);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null)
            return $"Order '{orderId}' not found.";

        if (order.Shipment is null)
            return $"Order '{orderId}' has not shipped yet. Current status: {order.Status}. " +
                   $"Orders typically ship within 1-2 business days of placement.";

        var deliveryStatus = order.Status switch
        {
            OrderStatus.Delivered => "Delivered",
            OrderStatus.OutForDelivery => "Out for delivery today",
            OrderStatus.Shipped => $"In transit — Est. delivery: {order.Shipment.EstimatedDelivery:MMM dd, yyyy}",
            _ => order.Status.ToString()
        };

        return $"Tracking for order {orderId}:\n" +
               $"  Carrier: {order.Shipment.Carrier}\n" +
               $"  Tracking #: {order.Shipment.TrackingNumber}\n" +
               $"  Shipped: {order.Shipment.ShippedAt:MMM dd, yyyy}\n" +
               $"  Current Location: {order.Shipment.CurrentLocation}\n" +
               $"  Status: {deliveryStatus}";
    }

    [Description("Report that a shipment appears to be lost or significantly delayed. " +
                 "This creates an investigation case with the carrier.")]
    public async Task<string> ReportLostShipmentAsync(
        [Description("The order ID for the lost/delayed shipment")] string orderId,
        [Description("Additional details from the customer about the issue")] string details)
    {
        logger.LogInformation("ReportLostShipment for {OrderId}", orderId);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null) return $"Order '{orderId}' not found.";

        if (order.Shipment is null)
            return $"Order '{orderId}' has not shipped yet — it cannot be reported as lost.";

        if (order.Status == OrderStatus.Delivered)
            return $"Our records show order '{orderId}' was delivered. " +
                   $"If you haven't received it, please check with neighbors and your mailbox. " +
                   $"If still not found, we can escalate a carrier investigation.";

        var caseNumber = $"CASE-{Guid.NewGuid().ToString("N")[..6].ToUpper()}";

        return $"Lost shipment investigation opened.\n" +
               $"  Case #: {caseNumber}\n" +
               $"  Order: {orderId}\n" +
               $"  Carrier: {order.Shipment.Carrier}\n" +
               $"  Tracking: {order.Shipment.TrackingNumber}\n" +
               $"  Details recorded: {details}\n\n" +
               $"The carrier has been notified. You will receive an email update within 2 business days. " +
               $"If the shipment is confirmed lost, a replacement or full refund will be issued.";
    }

    [Description("Request a change of delivery address for an order that has not yet shipped.")]
    public async Task<string> RequestAddressChangeAsync(
        [Description("The order ID")] string orderId,
        [Description("The new delivery address")] string newAddress)
    {
        logger.LogInformation("RequestAddressChange for {OrderId}", orderId);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null) return $"Order '{orderId}' not found.";

        if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Processing)
            return $"Order '{orderId}' has already been dispatched (status: {order.Status}) " +
                   $"and the address cannot be changed. " +
                   $"Please contact {order.Shipment?.Carrier ?? "the carrier"} directly with tracking# " +
                   $"{order.Shipment?.TrackingNumber ?? "N/A"}.";

        return $"Address change request received for order '{orderId}'.\n" +
               $"  New address: {newAddress}\n" +
               $"  Your request is being processed. You'll receive a confirmation email shortly.";
    }
}
