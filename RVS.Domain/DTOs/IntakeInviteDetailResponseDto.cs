namespace RVS.Domain.DTOs;

/// <summary>
/// An advisor intake invite as returned on create and by id (<c>Spec A-14</c>, issue #663).
/// </summary>
public sealed record IntakeInviteDetailResponseDto : IntakeInviteSummaryResponseDto
{
    /// <summary>
    /// The prefilled intake URL for a self-entry invite, for the advisor to open. <c>null</c> for a
    /// texted invite, whose link went to the caller and is not handed back, and on any read after
    /// the create: the raw token is never stored, so the URL cannot be rebuilt.
    /// </summary>
    public string? IntakeUrl { get; init; }
}
