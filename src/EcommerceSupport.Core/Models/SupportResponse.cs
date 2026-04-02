namespace EcommerceSupport.Core.Models;

public record SupportRequest(
    string SessionId,
    string CustomerId,
    string Message,
    string? PreviousContext = null
);

public record SupportResponse(
    string SessionId,
    string TicketId,
    string Message,
    SupportDepartment HandledBy,
    TriageResult Triage,
    DateTime RespondedAt
)
{
    public static SupportResponse From(
        SupportRequest request,
        string ticketId,
        string message,
        TriageResult triage) =>
        new(
            request.SessionId,
            ticketId,
            message,
            triage.ToDepartment(),
            triage,
            DateTime.UtcNow);
}
