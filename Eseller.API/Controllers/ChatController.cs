using Eseller.Application.Features.Chat.GetChatMessages;
using Eseller.Application.Features.Chat.GetConversations;
using Eseller.Application.Features.Chat.GetUnreadCount;
using Eseller.Application.Features.Chat.MarkMessageAsRead;
using Eseller.Application.Features.Chat.SendChatMessage;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eseller.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly ISender _sender;

    public ChatController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Get chat messages for an order.
    /// </summary>
    [HttpGet("{orderId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid orderId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var query = new GetChatMessagesQuery(orderId, pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Send a message to an order chat.
    /// </summary>
    [HttpPost("{orderId:guid}/messages")]
    public async Task<IActionResult> SendMessage(
        Guid orderId,
        [FromBody] SendMessageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new SendChatMessageCommand(orderId, request.Message);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { messageId = result.Value, message = "Message sent." });
    }

    /// <summary>
    /// Mark message as read.
    /// </summary>
    [HttpPut("messages/{messageId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid messageId,
        CancellationToken cancellationToken)
    {
        var command = new MarkMessageAsReadCommand(messageId);
        var result = await _sender.Send(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "MESSAGE_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "ORDER_NOT_FOUND" => NotFound(new { error = result.Error, errorCode = result.ErrorCode }),
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(new { message = "Message marked as read." });
    }

    /// <summary>
    /// Get all conversations (admin only).
    /// </summary>
    [HttpGet("conversations")]
    [Authorize(Roles = "SuperAdmin,Partner")]
    public async Task<IActionResult> GetConversations(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new GetConversationsQuery(pageNumber, pageSize);
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ErrorCode switch
            {
                "FORBIDDEN" => StatusCode(StatusCodes.Status403Forbidden, new { error = result.Error, errorCode = result.ErrorCode }),
                _ => BadRequest(new { error = result.Error, errorCode = result.ErrorCode })
            };
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Get unread message count.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var query = new GetUnreadCountQuery();
        var result = await _sender.Send(query, cancellationToken);

        if (!result.IsSuccess)
            return BadRequest(new { error = result.Error, errorCode = result.ErrorCode });

        return Ok(new { unreadCount = result.Value });
    }
}

// ─────────────────────────────────────────────────────────────
// REQUEST DTOs
// ─────────────────────────────────────────────────────────────

public record SendMessageRequest(string Message);