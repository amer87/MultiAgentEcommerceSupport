using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Agents.Middleware;

/// <summary>
/// Logs every agent request and response for compliance and analytics.
///
/// In a production system, this would write to:
///   - Azure Application Insights (telemetry)
///   - An audit database table (ElasticSearch, Azure SQL, etc.)
///   - Azure Event Hub (for real-time analytics pipelines)
///
/// The middleware also measures latency per agent invocation.
/// Wired via AIAgentBuilder.Use(auditMiddleware.RunAsync, null).
/// </summary>
public sealed class AuditLoggingMiddleware(ILogger<AuditLoggingMiddleware> logger)
{
    /// <summary>
    /// Middleware delegate compatible with AIAgentBuilder.Use(runFunc, null).
    /// </summary>
    public async Task<AgentResponse> RunAsync(
        IEnumerable<ChatMessage> messages,
        AgentSession? session,
        AgentRunOptions? options,
        AIAgent innerAgent,
        CancellationToken ct = default)
    {
        var start = DateTimeOffset.UtcNow;
        var sessionId = session?.StateBag.GetValue<string>("sessionId") ?? "no-session";
        var agentName = innerAgent.GetType().Name;
        var inputLen = 0;
        foreach (var m in messages) inputLen += m.Text?.Length ?? 0;

        logger.LogInformation(
            "[AUDIT] {AgentName} ← session={SessionId} | message_length={Len}",
            agentName, sessionId, inputLen);

        AgentResponse response;
        try
        {
            response = await innerAgent.RunAsync(messages, session, options, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "[AUDIT] {AgentName} ERROR in session={SessionId} after {Elapsed}ms",
                agentName, sessionId, (DateTimeOffset.UtcNow - start).TotalMilliseconds);
            throw;
        }

        var elapsed = (DateTimeOffset.UtcNow - start).TotalMilliseconds;

        logger.LogInformation(
            "[AUDIT] {AgentName} → session={SessionId} | elapsed={ElapsedMs}ms | response_length={Len}",
            agentName, sessionId, elapsed, response.Text?.Length ?? 0);

        // Structured log entry for analytics (token usage, latency histogram, etc.)
        logger.LogDebug(
            "[METRICS] agent={AgentName} session={SessionId} latency_ms={ElapsedMs} " +
            "input_tokens={InputTokens} output_tokens={OutputTokens}",
            agentName, sessionId, elapsed,
            response.Usage?.InputTokenCount ?? 0,
            response.Usage?.OutputTokenCount ?? 0);

        return response;
    }
}
