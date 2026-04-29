namespace RVS.Domain.Validation;

/// <summary>
/// Enforces valid status transitions for ServiceRequest entities.
/// Invalid transitions should return 409 Conflict from the API layer.
/// </summary>
public static class StatusTransitions
{
    private static readonly Dictionary<string, HashSet<string>> _allowed = new()
    {
        ["New"] = ["InProgress", "Completed", "Cancelled", "WaitingOnParts", "WaitingOnCustomer"],
        ["InProgress"] = ["New", "Completed", "Cancelled", "WaitingOnParts", "WaitingOnCustomer"],
        ["WaitingOnParts"] = ["New", "InProgress", "Completed", "Cancelled", "WaitingOnCustomer"],
        ["WaitingOnCustomer"] = ["New", "InProgress", "Completed", "Cancelled", "WaitingOnParts"],
        ["Completed"] = ["New", "InProgress", "Cancelled", "WaitingOnParts", "WaitingOnCustomer"],
        ["Cancelled"] = ["New", "InProgress", "Completed", "WaitingOnParts", "WaitingOnCustomer"],
    };

    /// <summary>
    /// Returns the set of statuses that can be reached from <paramref name="currentStatus"/>.
    /// </summary>
    /// <param name="currentStatus">The current status value.</param>
    /// <returns>A read-only collection of valid target statuses, or empty if none are allowed.</returns>
    public static IReadOnlyCollection<string> GetAllowedTargets(string currentStatus)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(currentStatus);

        return _allowed.TryGetValue(currentStatus, out var targets) ? targets : [];
    }

    /// <summary>
    /// Returns true if the transition from <paramref name="from"/> to <paramref name="to"/> is valid.
    /// </summary>
    /// <param name="from">The current status value.</param>
    /// <param name="to">The desired target status value.</param>
    /// <returns><c>true</c> if the transition is allowed; otherwise <c>false</c>.</returns>
    public static bool IsValid(string from, string to)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(from);
        ArgumentException.ThrowIfNullOrWhiteSpace(to);

        return _allowed.TryGetValue(from, out var targets) && targets.Contains(to);
    }
}
