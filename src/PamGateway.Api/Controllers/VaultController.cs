using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PamGateway.Core;

namespace PamGateway.Api.Controllers;

[ApiController]
[Route("api/v1/vault")]
[Authorize]
public sealed class VaultController : ControllerBase
{
    private readonly ICredentialStore _credentials;
    private readonly ICredentialCheckoutStore _checkouts;
    private readonly IAuditStore _audit;

    public VaultController(
        ICredentialStore credentials,
        ICredentialCheckoutStore checkouts,
        IAuditStore audit)
    {
        _credentials = credentials;
        _checkouts = checkouts;
        _audit = audit;
    }

    [HttpGet("credentials")]
    public IActionResult GetAll()
    {
        var list = _credentials.GetAll().Select(c => new
        {
            c.Id, c.TargetId, c.Username, c.Status,
            c.CreatedAt, c.LastRotatedAt, c.LastCheckedOutAt,
            c.CheckedOutBy, c.IsBreakGlass, c.RotationIntervalHours
        });
        return Ok(list);
    }

    [HttpGet("credentials/{id}")]
    public IActionResult GetById(string id)
    {
        var cred = _credentials.GetById(id);
        if (cred is null) return NotFound(new { message = "Credential not found" });
        return Ok(new
        {
            cred.Id, cred.TargetId, cred.Username, cred.Status,
            cred.CreatedAt, cred.LastRotatedAt, cred.LastCheckedOutAt,
            cred.CheckedOutBy, cred.IsBreakGlass, cred.RotationIntervalHours
        });
    }

    [HttpPost("credentials")]
    public IActionResult Create([FromBody] CredentialCreateDto dto)
    {
        var encrypted = EncryptPassword(dto.Password);
        var cred = new Credential(
            $"CRED-{Guid.NewGuid():N}",
            dto.TargetId,
            dto.Username,
            encrypted,
            CredentialStatus.Available,
            DateTimeOffset.UtcNow,
            null, null, null,
            dto.IsBreakGlass,
            dto.RotationIntervalHours > 0 ? dto.RotationIntervalHours : 24
        );
        _credentials.Add(cred);

        _audit.Add(AuditEventFactory.Create(HttpContext, "vault.credential.created",
            $"Created credential for {dto.Username}@{dto.TargetId}", "success",
            targetId: dto.TargetId));

        return CreatedAtAction(nameof(GetById), new { id = cred.Id }, new
        {
            cred.Id, cred.TargetId, cred.Username, cred.Status,
            cred.CreatedAt, cred.IsBreakGlass, cred.RotationIntervalHours
        });
    }

    [HttpPost("credentials/{id}/checkout")]
    public IActionResult Checkout(string id, [FromBody] CheckoutDto dto)
    {
        var cred = _credentials.GetById(id);
        if (cred is null) return NotFound(new { message = "Credential not found" });

        if (cred.Status == CredentialStatus.Disabled)
            return Conflict(new { message = "Credential is disabled" });

        if (cred.Status == CredentialStatus.CheckedOut)
            return Conflict(new { message = "Credential is already checked out" });

        if (cred.Status == CredentialStatus.Rotating)
            return Conflict(new { message = "Credential is being rotated" });

        var username = User.Identity?.Name ?? "unknown";
        var checkout = new CredentialCheckout(
            $"CO-{Guid.NewGuid():N}",
            id, username, DateTimeOffset.UtcNow, null, dto.Reason
        );
        _checkouts.Add(checkout);

        var updated = cred with
        {
            Status = CredentialStatus.CheckedOut,
            LastCheckedOutAt = DateTimeOffset.UtcNow,
            CheckedOutBy = username
        };
        _credentials.Update(updated);

        var eventType = cred.IsBreakGlass ? "vault.breakglass.checkout" : "vault.credential.checkout";
        _audit.Add(AuditEventFactory.Create(HttpContext, eventType,
            $"Checked out {cred.Username}@{cred.TargetId}", "success",
            targetId: cred.TargetId));

        var password = DecryptPassword(cred.EncryptedPassword);
        return Ok(new { checkoutId = checkout.Id, username = cred.Username, password, expiresIn = "session" });
    }

    [HttpPost("credentials/{id}/checkin")]
    public IActionResult Checkin(string id)
    {
        var cred = _credentials.GetById(id);
        if (cred is null) return NotFound(new { message = "Credential not found" });

        if (cred.Status != CredentialStatus.CheckedOut)
            return Conflict(new { message = "Credential is not checked out" });

        var activeCheckout = _checkouts.GetByCredentialId(id)
            .FirstOrDefault(c => c.CheckedInAt is null);

        if (activeCheckout is not null)
        {
            _checkouts.Update(activeCheckout with { CheckedInAt = DateTimeOffset.UtcNow });
        }

        _credentials.Update(cred with { Status = CredentialStatus.Available, CheckedOutBy = null });

        _audit.Add(AuditEventFactory.Create(HttpContext, "vault.credential.checkin",
            $"Checked in {cred.Username}@{cred.TargetId}", "success",
            targetId: cred.TargetId));

        return Ok(new { message = "Credential checked in" });
    }

    [HttpPost("credentials/{id}/rotate")]
    public IActionResult Rotate(string id)
    {
        var cred = _credentials.GetById(id);
        if (cred is null) return NotFound(new { message = "Credential not found" });

        if (cred.Status == CredentialStatus.CheckedOut)
            return Conflict(new { message = "Cannot rotate while checked out" });

        var newPassword = GenerateSecurePassword(24);
        var encrypted = EncryptPassword(newPassword);
        _credentials.Update(cred with
        {
            EncryptedPassword = encrypted,
            LastRotatedAt = DateTimeOffset.UtcNow,
            Status = CredentialStatus.Available
        });

        _audit.Add(AuditEventFactory.Create(HttpContext, "vault.credential.rotated",
            $"Rotated password for {cred.Username}@{cred.TargetId}", "success",
            targetId: cred.TargetId));

        return Ok(new { message = "Password rotated", lastRotatedAt = DateTimeOffset.UtcNow });
    }

    [HttpGet("checkouts")]
    public IActionResult GetCheckouts([FromQuery] string? credentialId)
    {
        var all = string.IsNullOrWhiteSpace(credentialId)
            ? _checkouts.GetAll()
            : _checkouts.GetByCredentialId(credentialId);
        return Ok(all);
    }

    private static string EncryptPassword(string password)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(password));

    private static string DecryptPassword(string encrypted)
        => Encoding.UTF8.GetString(Convert.FromBase64String(encrypted));

    private static string GenerateSecurePassword(int length)
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*";
        var data = new byte[length];
        RandomNumberGenerator.Fill(data);
        var sb = new StringBuilder(length);
        foreach (var b in data)
            sb.Append(chars[b % chars.Length]);
        return sb.ToString();
    }
}

public sealed record CredentialCreateDto(
    string TargetId,
    string Username,
    string Password,
    bool IsBreakGlass = false,
    int RotationIntervalHours = 24
);

public sealed record CheckoutDto(string Reason);
