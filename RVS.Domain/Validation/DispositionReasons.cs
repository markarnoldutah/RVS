namespace RVS.Domain.Validation;

/// <summary>
/// The fixed reason codes for closing a service request without work (<c>Spec C-4</c>,
/// issue #445). A disposition sets the request to <c>Cancelled</c> and records one of these
/// codes, which is what distinguishes it from a plain manual cancel or a <c>Completed</c> close.
///
/// The set is closed, the same for every location, in the order the Spec lists them. Codes are
/// stored verbatim (PascalCase, like the status values) and matched ordinally — there is no
/// normalisation, because the only writer is the manager app's picker.
/// </summary>
public static class DispositionReasons
{
    /// <summary>The request duplicates another one already on the board.</summary>
    public const string Duplicate = "Duplicate";

    /// <summary>Junk or abusive submission — not a real customer.</summary>
    public const string Spam = "Spam";

    /// <summary>Submitted to the wrong location (or the wrong dealership entirely).</summary>
    public const string WrongLocation = "WrongLocation";

    /// <summary>The customer called it off before any work was done.</summary>
    public const string CustomerWithdrew = "CustomerWithdrew";

    /// <summary>Every reason code, in <c>Spec C-4</c> order.</summary>
    public static readonly IReadOnlyList<string> All = [Duplicate, Spam, WrongLocation, CustomerWithdrew];

    private static readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal)
    {
        [Duplicate] = "Duplicate",
        [Spam] = "Spam",
        [WrongLocation] = "Wrong location",
        [CustomerWithdrew] = "Customer withdrew",
    };

    /// <summary>Whether <paramref name="code"/> is exactly one of <see cref="All"/>.</summary>
    public static bool IsValid(string? code) => code is not null && _labels.ContainsKey(code);

    /// <summary>
    /// The human label for a reason code, for the manager app. An unknown code comes back as-is
    /// (null as empty) so a value written by a later version still displays something.
    /// </summary>
    public static string GetLabel(string? code) =>
        code is not null && _labels.TryGetValue(code, out var label) ? label : code ?? string.Empty;
}
