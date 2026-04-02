using Azure.AI.OpenAI;
using Azure.Identity;
using EcommerceSupport.Agents.Memory;
using EcommerceSupport.Agents.Middleware;
using EcommerceSupport.Agents.Options;
using EcommerceSupport.Infrastructure.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EcommerceSupport.Agents.Factory;

/// <summary>
/// Creates and configures all the  agents.
/// </summary>
public sealed class AgentFactory(
    IOptions<AgentOptions> options,
    OrderTools orderTools,
    BillingTools billingTools,
    ShippingTools shippingTools,
    TechnicalTools technicalTools,
    CustomerContextProvider customerContext,
    AuditLoggingMiddleware auditMiddleware,
    RateLimitingMiddleware rateLimitMiddleware,
    ILogger<AgentFactory> logger)
{
    private readonly AgentOptions _opts = options.Value;

    // Wraps an agent with rate limiting, audit logging middleware.
    private AIAgent WireMiddleware(AIAgent agent) =>
        new AIAgentBuilder(agent)
            .Use(rateLimitMiddleware.RunAsync, null) // checked first from outside
            .Use(auditMiddleware.RunAsync, null)     // outermost: logs all requests
            .Build();

    // ─── Triage Agent ─────────────────────────────────────────────────────────

    /// <summary>
    /// Classifies the customer's intent into billing / shipping / technical / general.
    /// Returns structured JSON that maps to <see cref="Core.Models.TriageResult"/>.
    /// </summary>
    public AIAgent CreateTriageAgent()
    {
        var agent = CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "TriageAgent",
                ChatOptions = new()
                {
                    Instructions = """
                        You are a customer support triage specialist for an e-commerce store.
                        Your job is to analyze the customer's message and classify their intent.

                        Respond ONLY with a JSON object matching this schema:
                        {
                          "intent":          "billing" | "shipping" | "technical" | "general",
                          "confidence":      0.0-1.0,
                          "summary":         "one-line summary of what the customer needs",
                          "mentionedOrderId": "ORD-XXXXX" | null,
                          "urgency":         "low" | "medium" | "high"
                        }

                        Intent definitions:
                        - billing:    refunds, charges, invoices, payment issues, pricing
                        - shipping:   delivery tracking, lost packages, address changes, delays
                        - technical:  product not working, setup help, compatibility questions
                        - general:    everything else (policies, account, product info)

                        Set urgency=high if the customer expresses frustration, anger, or time-sensitive issues.
                        """,
                    MaxOutputTokens = 256
                },
                AIContextProviders = [customerContext]
            });
        return WireMiddleware(agent);
    }

    // ─── Billing Agent ────────────────────────────────────────────────────────

    public AIAgent CreateBillingAgent()
    {
        var agent = CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "BillingAgent",
                ChatOptions = new()
                {
                    Instructions = """
                        You are a billing specialist for an e-commerce store.
                        You handle: refund requests, charge disputes, invoice questions,
                        payment method issues, and promotional discounts.

                        Guidelines:
                        - Always verify refund eligibility before promising a refund.
                        - Be empathetic and professional, especially with frustrated customers.
                        - If a refund is approved, confirm the amount and timeline (3-5 business days).
                        - Never share other customers' data.
                        - If something is outside your authority, escalate to a manager.

                        You have access to tools to check orders, verify eligibility, and process refunds.
                        Always use tools to get accurate data — never guess order details.
                        """,
                    MaxOutputTokens = _opts.MaxTokens,
                    Tools =
                    [
                        AIFunctionFactory.Create(billingTools.CheckRefundEligibilityAsync),
                        AIFunctionFactory.Create(billingTools.ProcessRefundAsync),
                        AIFunctionFactory.Create(billingTools.GetBillingSummaryAsync),
                        AIFunctionFactory.Create(orderTools.GetOrderDetailsAsync),
                        AIFunctionFactory.Create(orderTools.GetRecentOrdersAsync)
                    ]
                },
                AIContextProviders = [customerContext]
            });
        return WireMiddleware(agent);
    }

    // ─── Shipping Agent ───────────────────────────────────────────────────────

    public AIAgent CreateShippingAgent()
    {
        var agent = CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "ShippingAgent",
                ChatOptions = new()
                {
                    Instructions = """
                        You are a shipping and logistics specialist for an e-commerce store.
                        You handle: delivery tracking, lost/delayed packages, address changes,
                        carrier disputes, and estimated delivery questions.

                        Guidelines:
                        - Always look up tracking info before giving delivery updates.
                        - If a package is lost (>5 days past estimated delivery), open an investigation.
                        - Address changes are only possible before shipment.
                        - Be proactive: offer solutions rather than just stating problems.
                        - Provide specific tracking numbers and carrier contact info when relevant.
                        """,
                    MaxOutputTokens = _opts.MaxTokens,
                    Tools =
                    [
                        AIFunctionFactory.Create(shippingTools.GetTrackingInfoAsync),
                        AIFunctionFactory.Create(shippingTools.ReportLostShipmentAsync),
                        AIFunctionFactory.Create(shippingTools.RequestAddressChangeAsync),
                        AIFunctionFactory.Create(orderTools.GetOrderDetailsAsync),
                        AIFunctionFactory.Create(orderTools.TrackShipmentAsync)
                    ]
                },
                AIContextProviders = [customerContext]
            });
        return WireMiddleware(agent);
    }

    // ─── Technical Agent ──────────────────────────────────────────────────────

    public AIAgent CreateTechnicalAgent()
    {
        var agent = CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "TechnicalAgent",
                ChatOptions = new()
                {
                    Instructions = """
                        You are a technical support specialist for an e-commerce store.
                        You help customers with: product setup, troubleshooting malfunctions,
                        compatibility questions, firmware updates, and defective product claims.

                        Guidelines:
                        - Start with the simplest troubleshooting steps, then escalate.
                        - Ask clarifying questions if the issue is ambiguous.
                        - Check compatibility before suggesting workarounds.
                        - If basic troubleshooting fails, create an escalation ticket.
                        - For defective products still under warranty, offer replacement.
                        """,
                    MaxOutputTokens = _opts.MaxTokens,
                    Tools =
                    [
                        AIFunctionFactory.Create(technicalTools.GetTroubleshootingStepsAsync),
                        AIFunctionFactory.Create(technicalTools.CheckCompatibilityAsync),
                        AIFunctionFactory.Create(technicalTools.EscalateToTechnicianAsync),
                        AIFunctionFactory.Create(orderTools.GetOrderDetailsAsync)
                    ]
                },
                AIContextProviders = [customerContext]
            });
        return WireMiddleware(agent);
    }

    // ─── General / Fallback Agent ─────────────────────────────────────────────

    public AIAgent CreateGeneralAgent()
    {
        var agent = CreateChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "GeneralAgent",
                ChatOptions = new()
                {
                    Instructions = """
                        You are a helpful customer support agent for an e-commerce store.
                        You handle general inquiries about: store policies, account management,
                        product information, promotions, and questions not related to specific orders.

                        Guidelines:
                        - Be friendly, concise, and helpful.
                        - For account-specific issues, look up order history if needed.
                        - Our return policy: 30 days for most items, 14 days for electronics.
                        - Free shipping on orders over $50. Standard shipping 2-5 business days.
                        - If the question is outside your knowledge, say so honestly and offer to escalate.
                        """,
                    MaxOutputTokens = _opts.MaxTokens,
                    Tools =
                    [
                        AIFunctionFactory.Create(orderTools.GetRecentOrdersAsync),
                        AIFunctionFactory.Create(orderTools.GetOrderDetailsAsync)
                    ]
                },
                AIContextProviders = [customerContext]
            });
        return WireMiddleware(agent);
    }

    // ─── Private Helpers ──────────────────────────────────────────────────────

    private IChatClient CreateChatClient()
    {
        logger.LogDebug("Creating AzureOpenAI chat client for endpoint {Endpoint}, model {Model}",
            _opts.AzureOpenAIEndpoint, _opts.DeploymentName);

        return new AzureOpenAIClient(
                new Uri(_opts.AzureOpenAIEndpoint),
                new DefaultAzureCredential())
            .GetChatClient(_opts.DeploymentName)
            .AsIChatClient();
    }
}
