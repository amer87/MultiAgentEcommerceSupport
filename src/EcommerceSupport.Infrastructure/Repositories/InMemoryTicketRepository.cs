using System.Collections.Concurrent;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Infrastructure.Repositories;
// TODO : Change to real database
public sealed class InMemoryTicketRepository : ITicketRepository
{
    private readonly ConcurrentDictionary<string, SupportTicket> _tickets = new();

    public Task<SupportTicket> CreateAsync(SupportTicket ticket, CancellationToken ct = default)
    {
        _tickets[ticket.TicketId] = ticket;
        return Task.FromResult(ticket);
    }

    public Task<SupportTicket?> GetByIdAsync(string ticketId, CancellationToken ct = default)
        => Task.FromResult(_tickets.GetValueOrDefault(ticketId));

    public Task<IReadOnlyList<SupportTicket>> GetByCustomerIdAsync(
        string customerId, CancellationToken ct = default)
    {
        IReadOnlyList<SupportTicket> result = [.. _tickets.Values
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.OpenedAt)];
        return Task.FromResult(result);
    }

    public Task UpdateAsync(SupportTicket ticket, CancellationToken ct = default)
    {
        _tickets[ticket.TicketId] = ticket;
        return Task.CompletedTask;
    }

    public Task AddMessageAsync(string ticketId, TicketMessage message, CancellationToken ct = default)
    {
        if (_tickets.TryGetValue(ticketId, out var ticket))
            ticket.Messages.Add(message);
        return Task.CompletedTask;
    }
}
