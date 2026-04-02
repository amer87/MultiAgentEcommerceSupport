using System.ComponentModel.DataAnnotations;

namespace EcommerceSupport.Api.DTOs;

/// <summary>Request body for POST /api/sessions/{sessionId}/messages</summary>
public sealed record ChatRequest(
    [Required, MinLength(1), MaxLength(2000)]
    string Message
);

/// <summary>Request body for POST /api/sessions (create a new session)</summary>
public sealed record CreateSessionRequest(
    [Required] string CustomerId
);
