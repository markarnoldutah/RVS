using System.Text;

namespace RVS.UI.Shared.Components;

/// <summary>
/// Pure presentation helper for <see cref="StatusBadge"/>: maps a stored service-request
/// status to its CSS token class. Recognises the decided vocabulary (<c>Spec C-3</c> / C-8,
/// issue #428): <c>New</c>, <c>InProgress</c>, <c>WaitingOnParts</c>, <c>WaitingOnCustomer</c>,
/// <c>Completed</c>, <c>Cancelled</c> (spacing, hyphens and underscores are ignored, so
/// display-style values like "In Progress" still match). Anything unrecognised, null, or
/// blank gets the default token class.
/// </summary>
public static class StatusBadgeFormatting
{
    private const string DefaultCssClass = "rvs-status-default";

    public static string GetCssClass(string? status) => Canonicalize(status) switch
    {
        "new" => "rvs-status-new",
        "inprogress" => "rvs-status-in-progress",
        "waitingonparts" => "rvs-status-waiting-on-parts",
        "waitingoncustomer" => "rvs-status-waiting-on-customer",
        "completed" => "rvs-status-completed",
        "cancelled" => "rvs-status-cancelled",
        _ => DefaultCssClass
    };

    private static string Canonicalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(status.Length);
        foreach (var ch in status)
        {
            if (ch is ' ' or '-' or '_')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }
}
