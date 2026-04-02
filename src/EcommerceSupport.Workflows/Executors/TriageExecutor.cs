using System.Text.Json;
using EcommerceSupport.Core.Models;
using EcommerceSupport.Workflows.Models;
using EcommerceSupport.Workflows.Abstractions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Workflows.Executors;

public sealed class TriageExecutor(AIAgent triageAgent, ILogger<TriageExecutor> logger)
    : Executor<SupportRequest, WorkflowContext>("TriageExecutor")
{
    public override async ValueTask<WorkflowContext> HandleAsync(
        SupportRequest request,
        IWorkflowContext context,
        CancellationToken ct = default)
    {
        logger.LogInformation(
            "[TRIAGE] Classifying message for customer {CustomerId} in session {SessionId}",
            request.CustomerId, request.SessionId);

        // Create an ephemeral session for triage (stateless, no memory needed)
        var session = await triageAgent.CreateSessionAsync(ct);

        // Inject the customerId and sessionId into StateBag so CustomerContextProvider can load it
        session.StateBag.SetValue("customerId", request.CustomerId);
        session.StateBag.SetValue("sessionId", request.SessionId);

        string triageJson;
        try
        {
            var triageResponse = await triageAgent.RunAsync(
                [new ChatMessage(ChatRole.User, $"Customer message: {request.Message}")],
                session,
                null,
                ct);
            triageJson = triageResponse.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[TRIAGE] TriageAgent failed — falling back to General intent");
            triageJson = """{"intent":"general","confidence":0.5,"summary":"Unable to classify","urgency":"medium"}""";
        }

        var triage = ParseTriageResult(triageJson);

        logger.LogInformation(
            "[TRIAGE] Intent={Intent} Confidence={Confidence:P0} Urgency={Urgency} OrderId={OrderId}",
            triage.Intent, triage.Confidence, triage.Urgency, triage.MentionedOrderId);

        return new WorkflowContext(request, triage);
    }

    private TriageResult ParseTriageResult(string json)
    {
        try
        {
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start >= 0 && end > start)
                json = json[start..(end + 1)];

            return JsonSerializer.Deserialize<TriageResult>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new TriageResult { Intent = "general" };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "[TRIAGE] Failed to parse triage JSON: {Json}", json);
            return new TriageResult { Intent = "general", Confidence = 0.3f };
        }
    }
}
