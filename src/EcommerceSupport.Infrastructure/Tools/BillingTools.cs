using System.ComponentModel;
using EcommerceSupport.Core.Exceptions;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Infrastructure.Tools;

/// <summary>
/// Billing-specific tools exposed to the BillingAgent.
/// </summary>
public sealed class BillingTools(
    IOrderRepository orderRepository,
    ICustomerRepository customerRepository,
    ILogger<BillingTools> logger)
{
    private readonly Dictionary<string, decimal> _refundedOrders = [];

    [Description("Check whether an order is eligible for a refund. " +
                 "Returns eligibility status and the reason if ineligible.")]
    public async Task<string> CheckRefundEligibilityAsync(
        [Description("The order ID to check for refund eligibility")] string orderId)
    {
        logger.LogInformation("CheckRefundEligibility for {OrderId}", orderId);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null) return $"Order '{orderId}' not found.";

        if (_refundedOrders.ContainsKey(orderId))
            return $"Order '{orderId}' has already been refunded (amount: {_refundedOrders[orderId]:C}).";

        if (order.Status == OrderStatus.Refunded)
            return $"Order '{orderId}' has already been fully refunded.";

        if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Pending)
            return $"Order '{orderId}' is eligible for cancellation (still processing). " +
                   $"A full refund of {order.Total:C} would be issued.";

        var daysSincePlaced = (DateTime.UtcNow - order.PlacedAt).TotalDays;

        if (order.Status == OrderStatus.Delivered)
        {
            if (daysSincePlaced > 30)
                return $"Order '{orderId}' is NOT eligible for a refund. " +
                       $"The 30-day return window has expired (placed {(int)daysSincePlaced} days ago).";

            return $"Order '{orderId}' is eligible for a refund of {order.Total:C}. " +
                   $"The 30-day return window expires in {(int)(30 - daysSincePlaced)} days.";
        }

        if (order.Status == OrderStatus.Shipped)
            return $"Order '{orderId}' is in transit. " +
                   $"You can request a refund once it's delivered, or we can issue a return label.";

        return $"Order '{orderId}' with status '{order.Status}' is not eligible for a standard refund. " +
               $"Please provide more details for manual review.";
    }

    [Description("Process a refund for an eligible order. " +
                 "Only call this after confirming eligibility with the customer.")]
    public async Task<string> ProcessRefundAsync(
        [Description("The order ID to refund")] string orderId,
        [Description("The reason for the refund")] string reason)
    {
        logger.LogInformation("ProcessRefund for {OrderId}, reason: {Reason}", orderId, reason);

        var order = await orderRepository.GetByIdAsync(orderId);
        if (order is null) return $"Cannot process refund — order '{orderId}' not found.";

        if (_refundedOrders.ContainsKey(orderId))
            return $"Refund already processed for '{orderId}'.";

        var daysSincePlaced = (DateTime.UtcNow - order.PlacedAt).TotalDays;

        if (order.Status == OrderStatus.Delivered && daysSincePlaced > 30)
            throw new RefundNotEligibleException(orderId, "30-day return window has expired.");

        _refundedOrders[orderId] = order.Total;
        await orderRepository.UpdateStatusAsync(orderId, OrderStatus.Refunded, reason);

        return $"✅ Refund of {order.Total:C} successfully processed for order '{orderId}'. " +
               $"The amount will appear on the original payment method within 3-5 business days. " +
               $"Refund reason recorded: {reason}.";
    }

    [Description("Get a customer's billing summary including total spend and payment history.")]
    public async Task<string> GetBillingSummaryAsync(
        [Description("The customer ID")] string customerId)
    {
        logger.LogInformation("GetBillingSummary for {CustomerId}", customerId);

        var customer = await customerRepository.GetByIdAsync(customerId);
        if (customer is null) return $"Customer '{customerId}' not found.";

        var orders = await orderRepository.GetByCustomerIdAsync(customerId);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Billing Summary for {customer.FullName}:");
        sb.AppendLine($"  Membership Tier: {customer.Tier}");
        sb.AppendLine($"  Total Orders: {customer.TotalOrders}");
        sb.AppendLine($"  Lifetime Spend: {customer.TotalSpend:C}");
        sb.AppendLine();
        sb.AppendLine("Recent Charges:");
        foreach (var o in orders.Take(5))
            sb.AppendLine($"  • {o.OrderId} | {o.PlacedAt:yyyy-MM-dd} | {o.Total:C} | {o.Status}");

        return sb.ToString();
    }
}
