namespace RVS.Domain.DTOs;

/// <summary>
/// What the manager app's Send intake link dialog is allowed to do at a location right now
/// (<c>Spec A-14</c>, issues #666, #693). Read on open, so the dialog offers only the channels
/// that work and says texting is not enabled yet, instead of presenting a Send button that can only 409.
/// </summary>
public sealed record IntakeInviteCapabilityResponseDto
{
    /// <summary>
    /// <c>true</c> when the API will actually hand an invite to ACS. It is <c>false</c> in any
    /// environment whose toll-free number is not verified yet.
    /// </summary>
    public required bool SmsEnabled { get; init; }

    /// <summary>
    /// <c>true</c> when the API will actually hand an emailed invite to ACS (issue #693). It is
    /// <c>false</c> wherever email falls back to the no-op sender. Not <c>required</c>, so an
    /// older API that never sent it reads as email being unavailable.
    /// </summary>
    public bool EmailEnabled { get; init; }
}
