using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Workflows.Models;

/// <summary>
/// Carries both the original request and the triage classification
/// through the workflow graph, so all downstream executors have full context.
/// </summary>
public sealed record WorkflowContext(
    SupportRequest Request,
    TriageResult Triage
);
