using System.Text.Json;
using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using Microsoft.AspNetCore.Mvc;

namespace AgentFrameworkQuickStart.Api.Controllers;

/// <summary>
/// API controller for managing chat threads
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ThreadsController : ControllerBase
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ThreadsController> _logger;

    public ThreadsController(IUnitOfWork unitOfWork, ILogger<ThreadsController> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Get all chat threads
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ThreadDto>>> GetAll(
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default
    )
    {
        var threads = await _unitOfWork.Threads.GetAllAsync(includeArchived, cancellationToken);

        var dtos = threads.Select(t => new ThreadDto
        {
            Id = t.Id,
            Title = t.Title,
            Summary = t.Summary,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            IsArchived = t.IsArchived,
            MessageCount = t.Messages.Count,
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Get a specific thread with all messages
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ThreadWithMessagesDto>> GetById(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        var thread = await _unitOfWork.Threads.GetByIdWithMessagesAsync(id, cancellationToken);

        if (thread == null)
        {
            return NotFound(new { message = $"Thread {id} not found" });
        }

        var dto = new ThreadWithMessagesDto
        {
            Id = thread.Id,
            Title = thread.Title,
            Summary = thread.Summary,
            CreatedAt = thread.CreatedAt,
            UpdatedAt = thread.UpdatedAt,
            IsArchived = thread.IsArchived,
            Messages = thread
                .Messages.Select(m => new MessageDto
                {
                    Id = m.Id,
                    Role = m.Role.ToString().ToLower(),
                    Content = m.Content,
                    Timestamp = m.Timestamp,
                    SubAgentName = m.SubAgentName,
                    ToolCalls = string.IsNullOrEmpty(m.ToolCallsJson)
                        ? null
                        : JsonSerializer.Deserialize<object>(m.ToolCallsJson),
                    Metadata = string.IsNullOrEmpty(m.MetadataJson)
                        ? null
                        : JsonSerializer.Deserialize<object>(m.MetadataJson),
                    SequenceNumber = m.SequenceNumber,
                })
                .ToList(),
        };

        return Ok(dto);
    }

    /// <summary>
    /// Create a new chat thread
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ThreadDto>> Create(
        [FromBody] CreateThreadRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var thread = new ChatThread
        {
            Title = request.Title ?? "New Conversation",
            Summary = request.Summary,
        };

        var created = await _unitOfWork.Threads.CreateAsync(thread, cancellationToken);

        _logger.LogInformation(
            "Created new thread {ThreadId} with title '{Title}'",
            created.Id,
            created.Title
        );

        var dto = new ThreadDto
        {
            Id = created.Id,
            Title = created.Title,
            Summary = created.Summary,
            CreatedAt = created.CreatedAt,
            UpdatedAt = created.UpdatedAt,
            IsArchived = created.IsArchived,
            MessageCount = 0,
        };

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, dto);
    }

    /// <summary>
    /// Update a thread's title or summary
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ThreadDto>> Update(
        Guid id,
        [FromBody] UpdateThreadRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var thread = await _unitOfWork.Threads.GetByIdAsync(id, cancellationToken);

        if (thread == null)
        {
            return NotFound(new { message = $"Thread {id} not found" });
        }

        if (request.Title != null)
            thread.Title = request.Title;

        if (request.Summary != null)
            thread.Summary = request.Summary;

        var updated = await _unitOfWork.Threads.UpdateAsync(thread, cancellationToken);

        _logger.LogInformation("Updated thread {ThreadId}", id);

        var dto = new ThreadDto
        {
            Id = updated.Id,
            Title = updated.Title,
            Summary = updated.Summary,
            CreatedAt = updated.CreatedAt,
            UpdatedAt = updated.UpdatedAt,
            IsArchived = updated.IsArchived,
            MessageCount = 0,
        };

        return Ok(dto);
    }

    /// <summary>
    /// Delete a thread permanently
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken = default)
    {
        var thread = await _unitOfWork.Threads.GetByIdAsync(id, cancellationToken);

        if (thread == null)
        {
            return NotFound(new { message = $"Thread {id} not found" });
        }

        await _unitOfWork.Threads.DeleteAsync(id, cancellationToken);

        _logger.LogInformation("Deleted thread {ThreadId}", id);

        return NoContent();
    }

    /// <summary>
    /// Archive a thread (soft delete)
    /// </summary>
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult> Archive(Guid id, CancellationToken cancellationToken = default)
    {
        var thread = await _unitOfWork.Threads.GetByIdAsync(id, cancellationToken);

        if (thread == null)
        {
            return NotFound(new { message = $"Thread {id} not found" });
        }

        await _unitOfWork.Threads.ArchiveAsync(id, cancellationToken);

        _logger.LogInformation("Archived thread {ThreadId}", id);

        return Ok(new { message = "Thread archived successfully" });
    }

    /// <summary>
    /// Search threads by title or message content
    /// </summary>
    [HttpGet("search")]
    public async Task<ActionResult<IEnumerable<ThreadDto>>> Search(
        [FromQuery] string q,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(q))
        {
            return BadRequest(new { message = "Search query is required" });
        }

        var threads = await _unitOfWork.Threads.SearchAsync(q, cancellationToken);

        var dtos = threads.Select(t => new ThreadDto
        {
            Id = t.Id,
            Title = t.Title,
            Summary = t.Summary,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            IsArchived = t.IsArchived,
            MessageCount = t.Messages.Count,
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Add a message to a thread
    /// </summary>
    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<MessageDto>> AddMessage(
        Guid id,
        [FromBody] AddMessageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var thread = await _unitOfWork.Threads.GetByIdAsync(id, cancellationToken);

        if (thread == null)
        {
            return NotFound(new { message = $"Thread {id} not found" });
        }

        if (!Enum.TryParse<MessageRole>(request.Role, true, out var role))
        {
            return BadRequest(new { message = $"Invalid role: {request.Role}" });
        }

        var message = new ChatMessage
        {
            ThreadId = id,
            Role = role,
            Content = request.Content,
            SubAgentName = request.SubAgentName,
            ToolCallsJson =
                request.ToolCalls != null ? JsonSerializer.Serialize(request.ToolCalls) : null,
            MetadataJson =
                request.Metadata != null ? JsonSerializer.Serialize(request.Metadata) : null,
        };

        var created = await _unitOfWork.Messages.AddAsync(message, cancellationToken);

        // Update thread title from first user message if it's still "New Conversation"
        if (thread.Title == "New Conversation" && role == MessageRole.User)
        {
            thread.Title = GenerateThreadTitle(request.Content);
            await _unitOfWork.Threads.UpdateAsync(thread, cancellationToken);
        }

        var dto = new MessageDto
        {
            Id = created.Id,
            Role = created.Role.ToString().ToLower(),
            Content = created.Content,
            Timestamp = created.Timestamp,
            SubAgentName = created.SubAgentName,
            ToolCalls = request.ToolCalls,
            Metadata = request.Metadata,
            SequenceNumber = created.SequenceNumber,
        };

        return CreatedAtAction(nameof(GetById), new { id = id }, dto);
    }

    /// <summary>
    /// Generate a thread title from the first message
    /// </summary>
    private static string GenerateThreadTitle(string content)
    {
        // Take first 50 characters or first sentence, whichever is shorter
        var title = content.Length > 50 ? content[..50] + "..." : content;

        // Clean up the title
        var firstSentenceEnd = title.IndexOfAny(['.', '?', '!', '\n']);
        if (firstSentenceEnd > 0 && firstSentenceEnd < title.Length - 3)
        {
            title = title[..(firstSentenceEnd + 1)];
        }

        return title.Trim();
    }
}

#region DTOs

public record ThreadDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsArchived { get; init; }
    public int MessageCount { get; init; }
}

public record ThreadWithMessagesDto : ThreadDto
{
    public List<MessageDto> Messages { get; init; } = [];
}

public record MessageDto
{
    public Guid Id { get; init; }
    public string Role { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; }
    public string? SubAgentName { get; init; }
    public object? ToolCalls { get; init; }
    public object? Metadata { get; init; }
    public int SequenceNumber { get; init; }
}

public record CreateThreadRequest
{
    public string? Title { get; init; }
    public string? Summary { get; init; }
}

public record UpdateThreadRequest
{
    public string? Title { get; init; }
    public string? Summary { get; init; }
}

public record AddMessageRequest
{
    public string Role { get; init; } = "user";
    public string Content { get; init; } = string.Empty;
    public string? SubAgentName { get; init; }
    public object? ToolCalls { get; init; }
    public object? Metadata { get; init; }
}

#endregion
