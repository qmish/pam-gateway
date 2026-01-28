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
    private readonly RecordingOptions _options;

    public RecordingsController(IRecordingStore recordings, ISessionStore sessions, IOptions<RecordingOptions> options)
    {
        _recordings = recordings;
        _sessions = sessions;
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

    private bool IsAllowedMode(string mode)
        => _options.AllowedModes.Any(item => string.Equals(item, mode, StringComparison.OrdinalIgnoreCase));

    private static bool TryParseStatus(string status, out RecordingStatus recordingStatus)
        => Enum.TryParse(status, true, out recordingStatus);
}
