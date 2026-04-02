using EcommerceSupport.Api.DTOs;
using EcommerceSupport.Api.Services;
using EcommerceSupport.Core.Exceptions;
using EcommerceSupport.Core.Interfaces;
using EcommerceSupport.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EcommerceSupport.Api.Controllers;

/// <summary>
/// Customer Support Chat API
///
/// Typical call flow:
///   1. POST /api/sessions               → create session, get sessionId
///   2. POST /api/sessions/{id}/messages → send message, get AI response
///   3. DELETE /api/sessions/{id}         → end session when done
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class SupportController(
    ISupportWorkflow workflow,
    ISessionManager sessions,
    ITicketRepository tickets,
    ILogger<SupportController> logger) : ControllerBase
{

    /// <summary>Creates a new customer support session.</summary>
    [HttpPost("sessions")]
    [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult CreateSession([FromBody] CreateSessionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var session = sessions.CreateSession(request.CustomerId);

        logger.LogInformation(
            "[API] Session created: {SessionId} for customer {CustomerId}",
            session.SessionId, session.CustomerId);

        return CreatedAtAction(
            nameof(GetSession),
            new { sessionId = session.SessionId },
            new CreateSessionResponse(session.SessionId, session.CustomerId, session.CreatedAt));
    }

    /// <summary>Gets session information.</summary>
    [HttpGet("sessions/{sessionId}")]
    [ProducesResponseType(typeof(CreateSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetSession(string sessionId)
    {
        var session = sessions.GetSession(sessionId);
        if (session is null) return NotFound(new { error = "Session not found or expired." });

        return Ok(new CreateSessionResponse(
            session.SessionId, session.CustomerId, session.CreatedAt));
    }

    /// <summary>
    /// Sends a message to the support bot and receives a response.
    /// </summary>
    [HttpPost("sessions/{sessionId}/messages")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> SendMessage(
        string sessionId,
        [FromBody] ChatRequest request,
        CancellationToken ct = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var session = sessions.GetSession(sessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or expired. Please create a new session." });

        logger.LogInformation(
            "[API] Message received: session={SessionId} customer={CustomerId} length={Len}",
            sessionId, session.CustomerId, request.Message.Length);

        try
        {
            var supportRequest = new SupportRequest(
                SessionId: sessionId,
                CustomerId: session.CustomerId,
                Message: request.Message);

            var response = await workflow.HandleAsync(supportRequest, ct);

            return Ok(ChatResponseMapper.FromSupportResponse(response));
        }
        catch (WorkflowException ex)
        {
            logger.LogError(ex, "[API] Workflow error for session {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "The support system is temporarily unavailable. Please try again." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "[API] Unexpected error for session {SessionId}", sessionId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "An unexpected error occurred." });
        }
    }

    /// <summary>Ends a session and marks any open tickets as resolved.</summary>
    [HttpDelete("sessions/{sessionId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult EndSession(string sessionId)
    {
        var session = sessions.GetSession(sessionId);
        if (session is null)
            return NotFound(new { error = "Session not found or already ended." });

        sessions.EndSession(sessionId);

        logger.LogInformation(
            "[API] Session ended: {SessionId} for customer {CustomerId}",
            sessionId, session.CustomerId);

        return NoContent();
    }

    /// <summary>Retrieves all support tickets for a customer.</summary>
    [HttpGet("customers/{customerId}/tickets")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerTickets(
        string customerId, CancellationToken ct = default)
    {
        var customerTickets = await tickets.GetByCustomerIdAsync(customerId, ct);

        var result = customerTickets.Select(t => new
        {
            t.TicketId,
            t.Status,
            t.Department,
            t.Priority,
            t.Subject,
            t.RelatedOrderId,
            t.OpenedAt,
            t.ResolvedAt,
            MessageCount = t.Messages.Count
        });

        return Ok(result);
    }
}
