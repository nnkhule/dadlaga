using AttendanceSystem.Domain.Common;
using AttendanceSystem.Domain.Enums;

namespace AttendanceSystem.Domain.Entities;

/// <summary>
/// Localized notification template with variable placeholders.
/// </summary>
public class NotificationTemplate : BaseEntity
{
    public string Key { get; private set; } = string.Empty;
    public string Culture { get; private set; } = "mn-MN";
    public string Subject { get; private set; } = string.Empty;
    public string BodyTemplate { get; private set; } = string.Empty;
    public NotificationChannel DefaultChannel { get; private set; }

    private NotificationTemplate() { }

    public string RenderBody(IReadOnlyDictionary<string, string> variables)
    {
        var result = BodyTemplate;
        foreach (var (key, value) in variables)
            result = result.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        return result;
    }
}
