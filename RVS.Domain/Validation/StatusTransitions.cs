namespace RVS.Domain.Validation;

/// <summary>
/// Enforces valid status transitions for ServiceRequest entities.
/// Invalid transitions should return 409 Conflict from the API layer.
/// </summary>
public static class StatusTransitions
{
    private static readonly Dictionary<string, HashSet<string>> _allowed = new()
    {
        ["New"] = ["InProgress", "Cancelled"],
        ["InProgress"] = ["Completed", "OnHold", "Cancelled"],
        ["OnHold"] = ["InProgress", "Cancelled"],
        ["Completed"] = [],
        ["Cancelled"] = []
    };

    /// <summary>
    /// Returns true if the transition from <paramref name="from"/> to <paramref name="to"/> is valid.
    /// </summary>
    /// <param name="from">The current status value.</param>
    /// <param name="to">The desired target status value.</param>
    /// <returns><c>true</c> if the transition is allowed; otherwise <c>false</c>.</returns>
    public static bool IsValid(string from, string to) =>
        _allowed.TryGetValue(from, out var targets) && targets.Contains(to);
}
