namespace EcommerceSupport.Core.Models;

public enum TicketStatus { Open, InProgress, PendingCustomer, Resolved, Closed }
public enum TicketPriority { Low, Medium, High, Critical }
public enum SupportDepartment { General, Billing, Technical, Shipping }

public class SupportTicket
{
    public string TicketId { get; init; } = Guid.NewGuid().ToString("N")[..8].ToUpper();
    public string CustomerId { get; init; } = default!;
    public string SessionId { get; init; } = default!;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public SupportDepartment Department { get; set; } = SupportDepartment.General;
    public string Subject { get; set; } = default!;
    public string? RelatedOrderId { get; set; }
    public DateTime OpenedAt { get; init; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public List<TicketMessage> Messages { get; init; } = [];
}

public record TicketMessage(
    string Role,   // "user" | "agent"
    string Content,
    DateTime Timestamp
);
