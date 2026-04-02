using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;
using EcommerceSupport.Workflows.Models;
using EcommerceSupport.Workflows.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Workflows.Executors;

public abstract class DepartmentExecutor(
    string executorId,
    AIAgent agent,
    ITicketRepository ticketRepository,
    ILogger logger)
    : Executor<WorkflowContext, SupportResponse>(executorId)
{
    protected abstract SupportDepartment Department { get; }

    public override async ValueTask<SupportResponse> HandleAsync(
        WorkflowContext ctx,
        IWorkflowContext workflowCtx,
        CancellationToken ct = default)
    {
        var request = ctx.Request;
        var triage = ctx.Triage;

        logger.LogInformation(
            "[{ExecutorId}] Handling intent={Intent} for customer={CustomerId}",
            ExecutorId, triage.Intent, request.CustomerId);

        // Retrieve or create a persistent per-session agent session
        var agentSession = await GetOrCreateAgentSessionAsync(workflowCtx, request.SessionId, ct);

        // Inject customer ID and session ID into StateBag so CustomerContextProvider can load the profile
        agentSession.StateBag.SetValue("customerId", request.CustomerId);
        agentSession.StateBag.SetValue("sessionId", request.SessionId);

        // Build a context-enriched prompt for the department agent
        var enrichedMessage = BuildPrompt(request, triage);

        var agentResponse = await agent.RunAsync(
            [new ChatMessage(ChatRole.User, enrichedMessage)],
            agentSession,
            null,
            ct);
        var responseText = agentResponse.Text ?? string.Empty;

        // Persist the ticket message
        var ticket = await EnsureTicketAsync(request, triage, ct);
        await ticketRepository.AddMessageAsync(ticket.TicketId,
            new TicketMessage("user", request.Message, DateTime.UtcNow), ct);
        await ticketRepository.AddMessageAsync(ticket.TicketId,
            new TicketMessage("agent", responseText, DateTime.UtcNow), ct);

        return SupportResponse.From(request, ticket.TicketId, responseText, triage);
    }
    #region Helper Methods
    /// <summary>
    /// Stores the agent session in workflow shared state so it survives across turns.
    /// </summary>
    private async Task<AgentSession> GetOrCreateAgentSessionAsync(
        IWorkflowContext wfCtx, string sessionId, CancellationToken ct)
    {
        var stateKey = $"agent_session_{ExecutorId}_{sessionId}";

        if (wfCtx.TryGetState<AgentSession>(stateKey, out var existing) && existing is not null)
            return existing;

        var newSession = await agent.CreateSessionAsync(ct);
        wfCtx.SetState(stateKey, newSession);
        return newSession;
    }

    private static string BuildPrompt(SupportRequest request, TriageResult triage)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(request.Message);

        if (triage.MentionedOrderId is not null)
            sb.AppendLine($"[Note: Customer mentioned order {triage.MentionedOrderId}]");

        if (triage.Urgency == "high")
            sb.AppendLine("[Note: This is a HIGH urgency request — respond with extra care and urgency]");

        if (request.PreviousContext is not null)
            sb.AppendLine($"[Previous conversation context: {request.PreviousContext}]");

        return sb.ToString().Trim();
    }

    private async Task<SupportTicket> EnsureTicketAsync(
        SupportRequest request, TriageResult triage, CancellationToken ct)
    {
        var ticket = new SupportTicket
        {
            CustomerId = request.CustomerId,
            SessionId = request.SessionId,
            Department = Department,
            Priority = triage.ToTicketPriority(),
            Subject = triage.Summary ?? "Support Request",
            RelatedOrderId = triage.MentionedOrderId
        };

        return await ticketRepository.CreateAsync(ticket, ct);
    }
    #endregion
}

/// <summary>
/// Billing department executor - handles billing-related support requests as determined by the TriageAgent.
/// </summary>
/// <param name="agent"></param>
/// <param name="tickets"></param>
/// <param name="logger"></param>
public sealed class BillingExecutor(
    AIAgent agent, ITicketRepository tickets, ILogger<BillingExecutor> logger)
    : DepartmentExecutor("BillingExecutor", agent, tickets, logger)
{
    protected override SupportDepartment Department => SupportDepartment.Billing;
}

/// <summary>
/// Shipping department executor - handles shipping-related support requests as determined by the TriageAgent.
/// </summary>
/// <param name="agent"></param>
/// <param name="tickets"></param>
/// <param name="logger"></param>
public sealed class ShippingExecutor(
    AIAgent agent, ITicketRepository tickets, ILogger<ShippingExecutor> logger)
    : DepartmentExecutor("ShippingExecutor", agent, tickets, logger)
{
    protected override SupportDepartment Department => SupportDepartment.Shipping;
}

/// <summary>
/// Technical department executor - handles technical-related support requests as determined by the TriageAgent.
/// </summary>
/// <param name="agent"></param>
/// <param name="tickets"></param>
/// <param name="logger"></param>
public sealed class TechnicalExecutor(
    AIAgent agent, ITicketRepository tickets, ILogger<TechnicalExecutor> logger)
    : DepartmentExecutor("TechnicalExecutor", agent, tickets, logger)
{
    protected override SupportDepartment Department => SupportDepartment.Technical;
}

/// <summary>
/// General department executor - handles general support requests that don't fit other departments, as determined by the TriageAgent.
/// </summary>
/// <param name="agent"></param>
/// <param name="tickets"></param>
/// <param name="logger"></param>
public sealed class GeneralExecutor(
    AIAgent agent, ITicketRepository tickets, ILogger<GeneralExecutor> logger)
    : DepartmentExecutor("GeneralExecutor", agent, tickets, logger)
{
    protected override SupportDepartment Department => SupportDepartment.General;
}