using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Agents.Middleware;

/// <summary>
/// Enforces per-session rate limits to prevent abuse.
/// Default: max 20 requests per session per minute.
/// Wired via AIAgentBuilder.Use(rateLimitMiddleware.RunAsync, null).
/// </summary>
public sealed class RateLimitingMiddleware(
    ILogger<RateLimitingMiddleware> logger,
    int maxRequestsPerMinute = 20)
{
    private readonly ConcurrentDictionary<string, SessionRateData> _sessions = new();

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
        var sessionId = session?.StateBag.GetValue<string>("sessionId") ?? "anonymous";
        var data = _sessions.GetOrAdd(sessionId, _ => new SessionRateData());

        data.Cleanup();

        if (data.RequestCount >= maxRequestsPerMinute)
        {
            logger.LogWarning(
                "[RATE-LIMIT] Session {SessionId} exceeded {Limit} requests/min",
                sessionId, maxRequestsPerMinute);

            return new AgentResponse(new ChatMessage(
                ChatRole.Assistant,
                "I'm sorry, you've sent too many messages in a short period. " +
                "Please wait a moment before trying again."));
        }

        data.RecordRequest();
        return await innerAgent.RunAsync(messages, session, options, ct);
    }

    private sealed class SessionRateData
    {
        private readonly Queue<DateTimeOffset> _timestamps = new();

        public int RequestCount => _timestamps.Count;

        public void RecordRequest() => _timestamps.Enqueue(DateTimeOffset.UtcNow);

        public void Cleanup()
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-1);
            while (_timestamps.TryPeek(out var ts) && ts < cutoff)
                _timestamps.Dequeue();
        }
    }
}
