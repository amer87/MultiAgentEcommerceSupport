namespace EcommerceSupport.Core.Models;

public enum MembershipTier { Free, Silver, Gold, Platinum }

public record Address(
    string AddressLine1,
    string? AddressLine2,
    string PostalCode,
    string City,
    string Country
);

public class Customer
{
    public string CustomerId { get; init; } = default!;
    public string Email { get; init; } = default!;
    public string FullName { get; init; } = default!;
    public string? Phone { get; set; }
    public Address? DefaultAddress { get; set; }
    public MembershipTier Tier { get; set; } = MembershipTier.Free;
    public DateTime RegisteredAt { get; init; }
    public DateTime? LastLoginAt { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSpend { get; set; }
    public bool IsBlocked { get; set; }

    /// <summary>Summary injected into agent context to personalize responses.</summary>
    public string GetContextSummary() =>
        $"Customer: {FullName} (ID: {CustomerId}) | Tier: {Tier} | " +
        $"Total orders: {TotalOrders} | Total spend: {TotalSpend:C} | " +
        $"Member since: {RegisteredAt:yyyy-MM-dd}";
}
