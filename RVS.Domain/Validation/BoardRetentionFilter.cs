namespace RVS.Domain.Validation;

/// <summary>
/// Decides whether a service request belongs on the manager board (Spec C-10), so the Done
/// column holds recent work rather than the location's whole history.
///
/// Every open request is on the board, however old. A closed one (<c>Completed</c> or
/// <c>Cancelled</c>) stays only while it was last changed less than
/// <see cref="DoneColumnWindowDays"/> days ago; older closed work is found through
/// <c>/service-requests</c>.
/// </summary>
public static class BoardRetentionFilter
{
    /// <summary>How long a closed request stays in the board's Done column, in days.</summary>
    public const int DoneColumnWindowDays = 60;

    /// <summary>The statuses the Done column holds: <c>Completed</c> and <c>Cancelled</c>.</summary>
    public static IReadOnlyList<string> ClosedStatuses { get; } = ["Completed", "Cancelled"];

    private static readonly HashSet<string> _closedStatuses = new(ClosedStatuses, StringComparer.Ordinal);

    /// <summary>
    /// The instant a closed request must have been last changed <em>after</em> to stay on the board.
    /// The server scopes the board query with it (issue #849), so it draws the same line as
    /// <see cref="IsOnBoard"/>.
    /// </summary>
    /// <param name="nowUtc">The current time (UTC).</param>
    public static DateTime DoneColumnCutoffUtc(DateTime nowUtc) => nowUtc - TimeSpan.FromDays(DoneColumnWindowDays);

    /// <summary>
    /// Returns <c>true</c> when the request should be shown on the board.
    /// </summary>
    /// <param name="status">The request's C-3 status.</param>
    /// <param name="createdAtUtc">When the request was created (UTC); used when it has never been updated.</param>
    /// <param name="updatedAtUtc">When the request was last changed (UTC), or <c>null</c>.</param>
    /// <param name="nowUtc">The current time (UTC).</param>
    /// <exception cref="ArgumentException"><paramref name="status"/> is blank.</exception>
    public static bool IsOnBoard(string status, DateTime createdAtUtc, DateTime? updatedAtUtc, DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        if (!_closedStatuses.Contains(status))
        {
            return true;
        }

        var lastChangedUtc = DateTime.SpecifyKind(updatedAtUtc ?? createdAtUtc, DateTimeKind.Utc);
        return lastChangedUtc > DoneColumnCutoffUtc(nowUtc);
    }
}
