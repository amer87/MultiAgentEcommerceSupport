using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Infrastructure.Repositories;

// TODO : Change to real database
public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<string, Customer> _byId;
    private readonly Dictionary<string, Customer> _byEmail;

    public InMemoryCustomerRepository()
    {
        var customers = SeedData().ToList();
        _byId = customers.ToDictionary(c => c.CustomerId);
        _byEmail = customers.ToDictionary(c => c.Email, StringComparer.OrdinalIgnoreCase);
    }

    public Task<Customer?> GetByIdAsync(string customerId, CancellationToken ct = default)
        => Task.FromResult(_byId.GetValueOrDefault(customerId));

    public Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default)
        => Task.FromResult(_byEmail.GetValueOrDefault(email));

    public Task UpdateAsync(Customer customer, CancellationToken ct = default)
    {
        _byId[customer.CustomerId] = customer;
        _byEmail[customer.Email] = customer;
        return Task.CompletedTask;
    }

    private static IEnumerable<Customer> SeedData()
    {
        var now = DateTime.UtcNow;

        yield return new Customer
        {
            CustomerId = "CUST-001",
            Email = "jane.smith@example.com",
            FullName = "Jane Smith",
            Phone = "+1-206-555-0101",
            Tier = MembershipTier.Gold,
            RegisteredAt = now.AddYears(-2),
            TotalOrders = 14,
            TotalSpend = 1_847.32m,
            DefaultAddress = new Address("111 Main St", null, "B89 1QQ", "London", "UK")
        };

        yield return new Customer
        {
            CustomerId = "CUST-002",
            Email = "mark.johnson@example.com",
            FullName = "Mark Johnson",
            Tier = MembershipTier.Silver,
            RegisteredAt = now.AddMonths(-8),
            TotalOrders = 3,
            TotalSpend = 324.97m,
            DefaultAddress = new Address(
                "222 Main St", null, "B89 1QQ", "London", "UK")
        };

        yield return new Customer
        {
            CustomerId = "CUST-003",
            Email = "alice.chen@example.com",
            FullName = "Alice Chen",
            Tier = MembershipTier.Platinum,
            RegisteredAt = now.AddYears(-4),
            TotalOrders = 62,
            TotalSpend = 9_243.55m,
            DefaultAddress = new Address(
                "333 Main St", null, "B89 1QQ", "London", "UK")
        };

        yield return new Customer
        {
            CustomerId = "CUST-GUEST",
            Email = "guest@example.com",
            FullName = "Guest User",
            Tier = MembershipTier.Free,
            RegisteredAt = now
        };
    }
}
