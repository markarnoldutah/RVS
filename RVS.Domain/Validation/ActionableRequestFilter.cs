namespace RVS.Domain.Validation;

/// <summary>
/// Decides whether a service request belongs on the manager board's default "actionable"
/// view (issue #498, scope item 5), so opening the app lands on what needs a status change
/// today rather than on the whole history.
///
/// Every open request is actionable. A closed one (<c>Completed</c> or <c>Cancelled</c>) stays
/// visible only on the manager's local calendar day it was last changed, so work closed today
/// is still in view and anything older drops away.
/// </summary>
public static class ActionableRequestFilter
{
    private static readonly HashSet<string> _closedStatuses = new(StringComparer.Ordinal) { "Completed", "Cancelled" };

    /// <summary>
    /// Returns <c>true</c> when the request should be shown on the actionable board view.
    /// </summary>
    /// <param name="status">The request's C-3 status.</param>
    /// <param name="createdAtUtc">When the request was created (UTC); used when it has never been updated.</param>
    /// <param name="updatedAtUtc">When the request was last changed (UTC), or <c>null</c>.</param>
    /// <param name="now">The manager's current local time; its offset defines "today".</param>
    /// <exception cref="ArgumentException"><paramref name="status"/> is blank.</exception>
    public static bool IsActionable(string status, DateTime createdAtUtc, DateTime? updatedAtUtc, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (!_closedStatuses.Contains(status))
        {
            return true;
        }

        var lastChangedUtc = DateTime.SpecifyKind(updatedAtUtc ?? createdAtUtc, DateTimeKind.Utc);
        var lastChangedLocal = new DateTimeOffset(lastChangedUtc).ToOffset(now.Offset);
        return lastChangedLocal.Date == now.Date;
    }
}
