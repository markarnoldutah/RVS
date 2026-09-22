namespace RVS.Domain.DTOs;

/// <summary>
/// What an unexpired, unredeemed A-14 invite prefills on the intake form (<c>Spec A-14</c>,
/// issue #664): the caller's first name, phone and email as the advisor entered them, and nothing else.
/// Returned by the anonymous <c>GET api/intake/{slug}/invites/{token}</c>.
/// </summary>
public sealed record IntakeInvitePrefillResponseDto
{
    /// <summary>The caller's first name.</summary>
    public string FirstName { get; init; } = string.Empty;

    /// <summary>The caller's phone number in E.164, or <c>null</c> for a self-entry invite without one.</summary>
    public string? Phone { get; init; }

    /// <summary>The caller's email address, or <c>null</c> when the advisor did not enter one (issue #693).</summary>
    public string? Email { get; init; }
}
