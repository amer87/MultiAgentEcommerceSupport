using EcommerceSupport.Core.Models;

namespace EcommerceSupport.Api.DTOs;

public sealed record CreateSessionResponse(
    string SessionId,
    string CustomerId,
    DateTime CreatedAt
);

public sealed record ChatResponse(
    string SessionId,
    string TicketId,
    string Message,
    string HandledBy,
    TriageInfo Triage,
    DateTime RespondedAt
);

public sealed record TriageInfo(
    string Intent,
    float Confidence,
    string Summary,
    string? MentionedOrderId,
    string Urgency
);

public static class ChatResponseMapper
{
    public static ChatResponse FromSupportResponse(SupportResponse r) =>
        new(
            r.SessionId,
            r.TicketId,
            r.Message,
            r.HandledBy.ToString(),
            new TriageInfo(
                r.Triage.Intent,
                r.Triage.Confidence,
                r.Triage.Summary,
                r.Triage.MentionedOrderId,
                r.Triage.Urgency),
            r.RespondedAt);
}
