using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Core.Interfaces;

public interface ISupportWorkflow
{
    /// <summary>
    /// Processes a customer support request through the full triage-and-route pipeline.
    /// </summary>
    Task<SupportResponse> HandleAsync(SupportRequest request, CancellationToken ct = default);
}
