using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Core.Interfaces;

public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(string orderId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetByCustomerIdAsync(string customerId, int limit = 10, CancellationToken ct = default);
    Task<Order?> GetLatestOrderAsync(string customerId, CancellationToken ct = default);
    Task UpdateStatusAsync(string orderId, OrderStatus status, string? reason = null, CancellationToken ct = default);
}
