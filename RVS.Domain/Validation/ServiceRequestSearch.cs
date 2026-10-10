namespace RVS.Domain.Validation;

/// <summary>
/// The fixed vocabulary of the service-request search (issue #849). The server applies every
/// filter and returns the whole matching set; the manager app only sorts and pages it.
/// </summary>
/// <remarks>
/// Two scopes. <see cref="ListScope"/> (the default) backs <c>/service-requests</c> (<c>Spec C-1</c>):
/// every match, newest first, up to <see cref="MaxListResults"/>. <see cref="BoardScope"/> backs the
/// board (<c>Spec C-10</c>): every open request plus closed ones inside
/// <see cref="BoardRetentionFilter.DoneColumnWindowDays"/>, with no cap, because the board is a
/// bounded working set and a capped one would hide open work.
/// </remarks>
public static class ServiceRequestSearch
{
    /// <summary>The list scope: every match, newest first, up to <see cref="MaxListResults"/>.</summary>
    public const string ListScope = "List";

    /// <summary>The board scope: open work plus the Done window, uncapped.</summary>
    public const string BoardScope = "Board";

    /// <summary>
    /// Status-filter value for every request that is not Completed or Cancelled. Never stored on a
    /// request.
    /// </summary>
    public const string OpenStatusFilter = "Open";

    /// <summary>The most rows the list scope returns; past it the result is flagged as truncated.</summary>
    public const int MaxListResults = 500;

    /// <summary>Whether <paramref name="scope"/> is blank (the list) or exactly one of the scopes.</summary>
    public static bool IsValidScope(string? scope) =>
        string.IsNullOrWhiteSpace(scope)
        || string.Equals(scope, ListScope, StringComparison.Ordinal)
        || string.Equals(scope, BoardScope, StringComparison.Ordinal);

    /// <summary>Whether <paramref name="scope"/> is the board scope.</summary>
    public static bool IsBoardScope(string? scope) => string.Equals(scope, BoardScope, StringComparison.Ordinal);
}
