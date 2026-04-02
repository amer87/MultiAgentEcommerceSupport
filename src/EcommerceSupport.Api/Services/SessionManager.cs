using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EcommerceSupport.Api.Services;

/// <summary>
/// Manages customer support sessions.
///
/// A session tracks the customer ID and creation time.
/// Sessions expire after 30 minutes of inactivity.
///
/// In production, replace with a distributed cache (Redis) so sessions
/// survive API pod restarts.
/// </summary>
public interface ISessionManager
{
    SupportSession CreateSession(string customerId);
    SupportSession? GetSession(string sessionId);
    bool ValidateSession(string sessionId, string customerId);
    void EndSession(string sessionId);
}

public sealed record SupportSession(
    string SessionId,
    string CustomerId,
    DateTime CreatedAt
);

public sealed class InMemorySessionManager(
    IMemoryCache cache,
    ILogger<InMemorySessionManager> logger) : ISessionManager
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromMinutes(30);

    public SupportSession CreateSession(string customerId)
    {
        var session = new SupportSession(
            SessionId: Guid.NewGuid().ToString("N"),
            CustomerId: customerId,
            CreatedAt: DateTime.UtcNow);

        cache.Set(CacheKey(session.SessionId), session,
            new MemoryCacheEntryOptions
            {
                SlidingExpiration = SessionTtl,
                PostEvictionCallbacks =
                {
                    new PostEvictionCallbackRegistration
                    {
                        EvictionCallback = (key, _, reason, _) =>
                            logger.LogDebug("[SESSION] Evicted session {Key} reason={Reason}", key, reason)
                    }
                }
            });

        logger.LogInformation("[SESSION] Created session {SessionId} for customer {CustomerId}",
            session.SessionId, customerId);

        return session;
    }

    public SupportSession? GetSession(string sessionId) =>
        cache.TryGetValue<SupportSession>(CacheKey(sessionId), out var s) ? s : null;

    public bool ValidateSession(string sessionId, string customerId)
    {
        var session = GetSession(sessionId);
        return session is not null && session.CustomerId == customerId;
    }

    public void EndSession(string sessionId)
    {
        cache.Remove(CacheKey(sessionId));
        logger.LogInformation("[SESSION] Ended session {SessionId}", sessionId);
    }

    private static string CacheKey(string sessionId) => $"support_session:{sessionId}";
}
