namespace EcommerceSupport.Core.Models;

/// <summary>
/// The tyepd result from the TriageAgent.
/// </summary>
public class TriageResult
{
    /// <summary>billing | shipping | technical | general</summary>
    public string Intent { get; set; } = "general";

    /// <summary>0-1 confidence in the intent classification.</summary>
    public float Confidence { get; set; }

    /// <summary>What the customer needs.</summary>
    public string Summary { get; set; } = default!;

    /// <summary>Order ID extracted from the message. in case it was mentioned.</summary>
    public string? MentionedOrderId { get; set; }

    /// <summary>Detected urgency: low | medium | high</summary>
    public string Urgency { get; set; } = "medium";

    public SupportDepartment ToDepartment() => Intent.ToLowerInvariant() switch
    {
        "billing" => SupportDepartment.Billing,
        "shipping" => SupportDepartment.Shipping,
        "technical" => SupportDepartment.Technical,
        _ => SupportDepartment.General
    };

    public TicketPriority ToTicketPriority() => Urgency.ToLowerInvariant() switch
    {
        "high" => TicketPriority.High,
        "low" => TicketPriority.Low,
        _ => TicketPriority.Medium
    };
}
