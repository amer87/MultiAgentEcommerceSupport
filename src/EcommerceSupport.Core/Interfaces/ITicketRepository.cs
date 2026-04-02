using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Core.Interfaces;

public interface ITicketRepository
{
    Task<SupportTicket> CreateAsync(SupportTicket ticket, CancellationToken ct = default);
    Task<SupportTicket?> GetByIdAsync(string ticketId, CancellationToken ct = default);
    Task<IReadOnlyList<SupportTicket>> GetByCustomerIdAsync(string customerId, CancellationToken ct = default);
    Task UpdateAsync(SupportTicket ticket, CancellationToken ct = default);
    Task AddMessageAsync(string ticketId, TicketMessage message, CancellationToken ct = default);
}
