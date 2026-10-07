using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using AcxiomCRM.Data;
using AcxiomCRM.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AcxiomCRM.Services;

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService>? _logger;

    public AuditService(
        ApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService>? logger = null)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string module,
        string? entityName = null,
        string? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string result = "Success",
        string? userId = null,
        string? ipAddress = null)
    {
        try
        {
            var effectiveUserId = userId ?? GetCurrentUserId();
            var effectiveIp = ipAddress ?? GetCurrentIpAddress();

            var auditLog = new AuditLog
            {
                UserId = effectiveUserId,
                Action = action,
                Module = module,
                EntityName = entityName ?? module,
                RecordId = recordId,
                OldValue = SanitizeAndSerialize(oldValue),
                NewValue = SanitizeAndSerialize(newValue),
                Result = result,
                CreatedDate = DateTime.UtcNow,
                IpAddress = effectiveIp
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Fail-safe: Audit logging should not crash the core transaction if DB error,
            // but in production it is logged to server-side logs.
            _logger?.LogError(ex, "Failed to persist audit log entry for Action: {Action}, Module: {Module}", action, module);
        }
    }

    public async Task LogSecurityEventAsync(
        string action,
        string? userId,
        string result,
        string? details = null,
        string? ipAddress = null)
    {
        await LogAsync(
            action: action,
            module: "Auth",
            entityName: "ApplicationUser",
            recordId: userId,
            oldValue: null,
            newValue: details != null ? new { Details = details } : null,
            result: result,
            userId: userId,
            ipAddress: ipAddress);
    }

    private string? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null || !user.Identity?.IsAuthenticated == true)
        {
            return null;
        }
        return user.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private string? GetCurrentIpAddress()
    {
        return _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
    }

    private static string? SanitizeAndSerialize(object? obj)
    {
        if (obj == null) return null;
        if (obj is string str) return str;

        try
        {
            var json = JsonSerializer.Serialize(obj);
            var node = JsonNode.Parse(json);
            if (node is JsonObject jsonObject)
            {
                StripSensitiveProperties(jsonObject);
                return jsonObject.ToJsonString();
            }
            return json;
        }
        catch
        {
            return obj.ToString();
        }
    }

    private static void StripSensitiveProperties(JsonObject jsonObject)
    {
        var sensitiveKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Password", "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
            "Token", "Secret", "CurrentPassword", "NewPassword", "ConfirmPassword"
        };

        var keysToRemove = jsonObject
            .Where(kvp => sensitiveKeys.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            jsonObject.Remove(key);
        }

        foreach (var kvp in jsonObject)
        {
            if (kvp.Value is JsonObject childObject)
            {
                StripSensitiveProperties(childObject);
            }
        }
    }
}
