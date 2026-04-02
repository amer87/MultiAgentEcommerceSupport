using EcommerceSupport.Agents.Factory;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;
using EcommerceSupport.Workflows.Abstractions;
using EcommerceSupport.Workflows.Executors;
using EcommerceSupport.Workflows.Models;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Workflows;

/// <summary>
/// Orchestrates the workflow.
/// </summary>
public sealed class SupportWorkflow(
    AgentFactory agentFactory,
    ITicketRepository ticketRepository,
    ILoggerFactory loggerFactory) : ISupportWorkflow
{
    // Executors are stateless at the class level. Per-run state lives
    // in the InMemoryWorkflowContext created for each request.
    private readonly TriageExecutor _triage = new(
        agentFactory.CreateTriageAgent(),
        loggerFactory.CreateLogger<TriageExecutor>());

    private readonly BillingExecutor _billing = new(
        agentFactory.CreateBillingAgent(),
        ticketRepository,
        loggerFactory.CreateLogger<BillingExecutor>());

    private readonly ShippingExecutor _shipping = new(
        agentFactory.CreateShippingAgent(),
        ticketRepository,
        loggerFactory.CreateLogger<ShippingExecutor>());

    private readonly TechnicalExecutor _technical = new(
        agentFactory.CreateTechnicalAgent(),
        ticketRepository,
        loggerFactory.CreateLogger<TechnicalExecutor>());

    private readonly GeneralExecutor _general = new(
        agentFactory.CreateGeneralAgent(),
        ticketRepository,
        loggerFactory.CreateLogger<GeneralExecutor>());

    private readonly ILogger<SupportWorkflow> _logger =
        loggerFactory.CreateLogger<SupportWorkflow>();

    // ─── ISupportWorkflow ────────────────────────────────────────────────────

    public async Task<SupportResponse> HandleAsync(
        SupportRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[SupportWorkflow] customer={CustomerId} session={SessionId}",
            request.CustomerId, request.SessionId);

        // Each run gets isolated shared state (stores AgentSessions across steps)
        var context = new InMemoryWorkflowContext();

        // ── Step 1: Classify intent ──────────────────────────────────────────
        WorkflowContext triageCtx;
        try
        {
            triageCtx = await _triage.HandleAsync(request, context, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SupportWorkflow] Triage failed for session={SessionId}",
                request.SessionId);
            throw;
        }

        var intent = triageCtx.Triage.Intent.ToLowerInvariant();

        _logger.LogInformation(
            "[SupportWorkflow] Routed to intent={Intent} for session={SessionId}",
            intent, request.SessionId);

        // ── Step 2: Route to department executor ─────────────────────────────
        SupportResponse response;
        try
        {
            response = intent switch
            {
                "billing" => await _billing.HandleAsync(triageCtx, context, ct),
                "shipping" => await _shipping.HandleAsync(triageCtx, context, ct),
                "technical" => await _technical.HandleAsync(triageCtx, context, ct),
                _ => await _general.HandleAsync(triageCtx, context, ct)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[SupportWorkflow] Department executor failed for intent={Intent} session={SessionId}",
                intent, request.SessionId);
            throw;
        }

        _logger.LogInformation(
            "[SupportWorkflow] Completed — ticket={TicketId} session={SessionId}",
            response.TicketId, request.SessionId);

        return response;
    }
}
