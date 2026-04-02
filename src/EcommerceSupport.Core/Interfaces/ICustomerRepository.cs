using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Core.Interfaces;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(string customerId, CancellationToken ct = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task UpdateAsync(Customer customer, CancellationToken ct = default);
}
