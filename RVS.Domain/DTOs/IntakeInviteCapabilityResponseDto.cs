namespace RVS.Domain.DTOs;

/// <summary>
/// What the manager app's Send intake link dialog is allowed to do at a location right now
/// (<c>Spec A-14</c>, issue #666). Read on open, so the dialog can offer <i>Fill it in myself</i>
/// and say texting is not enabled yet, instead of presenting a Send button that can only 409.
/// </summary>
public sealed record IntakeInviteCapabilityResponseDto
{
    /// <summary>
    /// <c>true</c> when the API will actually hand an invite to ACS. It is <c>false</c> in any
    /// environment whose toll-free number is not verified yet.
    /// </summary>
    public required bool SmsEnabled { get; init; }
}
