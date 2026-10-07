namespace AcxiomCRM.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string module,
        string? entityName = null,
        string? recordId = null,
        object? oldValue = null,
        object? newValue = null,
        string result = "Success",
        string? userId = null,
        string? ipAddress = null);

    Task LogSecurityEventAsync(
        string action,
        string? userId,
        string result,
        string? details = null,
        string? ipAddress = null);
}
