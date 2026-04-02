using EcommerceSupport.Core.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Agents.Memory;
/// <summary>
/// Loads customer profile information into the agent context based on the customerId
/// </summary>
public sealed class CustomerContextProvider(
    ICustomerRepository customerRepository,
    ILogger<CustomerContextProvider> logger)
    : AIContextProvider
{
    // ─── Per-session state: customer ID resolved from the session ────────────
    private readonly ProviderSessionState<CustomerSessionData> _state =
        new(static _ => new CustomerSessionData(), nameof(CustomerContextProvider));

    public override string StateKey => _state.StateKey;

    // ─── Hook 1: After message — capture customer ID if not yet known ────────

    protected override async ValueTask StoreAIContextAsync(
        InvokedContext context, CancellationToken ct = default)
    {
        var data = _state.GetOrInitializeState(context.Session);
        if (data.CustomerId is not null) return; // already resolved

        // CustomerId is set in session StateBag by the executor layer
        var customerId = context.Session?.StateBag.GetValue<string>("customerId");
        if (!string.IsNullOrWhiteSpace(customerId))
        {
            data.CustomerId = customerId;
            data.Profile = await LoadProfileAsync(customerId, ct);
            _state.SaveState(context.Session, data);

            logger.LogInformation(
                "CustomerContextProvider: resolved customer {CustomerId}",
                customerId);
        }
    }

    // ─── Hook 2: Before LLM call — inject the profile as system context ──────

    protected override async ValueTask<AIContext> ProvideAIContextAsync(
        InvokingContext context, CancellationToken ct = default)
    {
        var data = _state.GetOrInitializeState(context.Session);

        if (data.Profile is null && data.CustomerId is not null)
            data.Profile = await LoadProfileAsync(data.CustomerId, ct);

        if (data.Profile is null)
            return new AIContext
            {
                Instructions = "The customer is not authenticated. Do not share order or account details."
            };

        return new AIContext
        {
            Instructions = $"""
                CUSTOMER CONTEXT (use this to personalize responses):
                {data.Profile}

                Always address the customer by their first name.
                Platinum/Gold tier customers should receive priority service.
                """
        };
    }

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<string?> LoadProfileAsync(string customerId, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct);
        return customer?.GetContextSummary();
    }

    private sealed class CustomerSessionData
    {
        public string? CustomerId { get; set; }
        public string? Profile { get; set; }
    }
}
