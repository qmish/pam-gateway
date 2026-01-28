using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/recordings")]
[Authorize]
public sealed class RecordingsController : ControllerBase
{
    private readonly IRecordingStore _recordings;
    private readonly ISessionStore _sessions;
    private readonly IRecordingStorage _storage;
    private readonly RecordingOptions _options;

    public RecordingsController(
        IRecordingStore recordings,
        ISessionStore sessions,
        IRecordingStorage storage,
        IOptions<RecordingOptions> options)
    {
        _recordings = recordings;
        _sessions = sessions;
        _storage = storage;
        _options = options.Value;
    }

    [HttpGet]
    public IActionResult GetAll() => Ok(_recordings.GetAll());

    [HttpGet("{id}")]
    public IActionResult GetById(string id)
    {
        var recording = _recordings.GetById(id);
        if (recording is null)
        {
            return NotFound(new { message = "Recording not found" });
        }

        return Ok(recording);
    }

    [HttpPost]
    public IActionResult Create([FromBody] RecordingCreateDto dto)
    {
        var session = _sessions.GetById(dto.SessionId);
        if (session is null)
        {
            return NotFound(new { message = "Session not found" });
        }

        var mode = string.IsNullOrWhiteSpace(dto.Mode) ? _options.DefaultMode : dto.Mode;
        if (!IsAllowedMode(mode))
        {
            return BadRequest(new { message = "Unsupported recording mode" });
        }

        var recording = new SessionRecording(
            $"REC-{Guid.NewGuid():N}",
            dto.SessionId,
            mode,
            dto.StorageUri,
            RecordingStatus.Recording,
            DateTimeOffset.UtcNow,
            null,
            null,
            null);

        _recordings.Add(recording);
        return CreatedAtAction(nameof(GetById), new { id = recording.Id }, recording);
    }

    [HttpPut("{id}")]
    public IActionResult Update(string id, [FromBody] RecordingUpdateDto dto)
    {
        if (!string.Equals(id, dto.Id, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Id mismatch" });
        }

        if (!TryParseStatus(dto.Status, out var status))
        {
            return BadRequest(new { message = "Invalid status" });
        }

        var existing = _recordings.GetById(id);
        if (existing is null)
        {
            return NotFound(new { message = "Recording not found" });
        }

        var updated = existing with
        {
            Status = status,
            EndedAt = dto.EndedAt ?? existing.EndedAt,
            SizeBytes = dto.SizeBytes ?? existing.SizeBytes,
            Hash = dto.Hash ?? existing.Hash,
            StorageUri = dto.StorageUri ?? existing.StorageUri
        };

        _recordings.Update(updated);
        return Ok(updated);
    }

    [HttpPost("{id}/content")]
    public async Task<IActionResult> UploadContent(string id, CancellationToken cancellationToken)
    {
        var recording = _recordings.GetById(id);
        if (recording is null)
        {
            return NotFound(new { message = "Recording not found" });
        }

        var result = await _storage.SaveAsync(recording.Id, Request.Body, cancellationToken);
        var updated = recording with
        {
            StorageUri = result.StorageUri,
            SizeBytes = result.SizeBytes,
            Hash = result.Hash,
            Status = RecordingStatus.Completed,
            EndedAt = DateTimeOffset.UtcNow
        };
        _recordings.Update(updated);
        return Ok(updated);
    }

    [HttpGet("{id}/content")]
    public async Task<IActionResult> DownloadContent(string id, CancellationToken cancellationToken)
    {
        var recording = _recordings.GetById(id);
        if (recording is null)
        {
            return NotFound(new { message = "Recording not found" });
        }

        if (string.IsNullOrWhiteSpace(recording.StorageUri))
        {
            return Conflict(new { message = "Recording content is not available" });
        }

        var stream = await _storage.OpenReadAsync(recording.StorageUri, cancellationToken);
        return File(stream, "application/octet-stream", $"{recording.Id}.bin");
    }

    private bool IsAllowedMode(string mode)
        => _options.AllowedModes.Any(item => string.Equals(item, mode, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseStatus(string status, out RecordingStatus recordingStatus)
        => Enum.TryParse(status, true, out recordingStatus);
}
