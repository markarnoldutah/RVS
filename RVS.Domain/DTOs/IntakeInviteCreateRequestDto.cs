using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs;

/// <summary>
/// Request to create an advisor intake invite (<c>Spec A-14</c>, issue #663): text or email
/// (issue #693) a prefilled, single-use intake link to a caller, or, with <see cref="SelfEntry"/>,
/// mint one for the advisor to fill in during the call.
/// </summary>
public sealed record IntakeInviteCreateRequestDto
{
    /// <summary>Longest first name accepted.</summary>
    public const int FirstNameMaxLength = 50;

    /// <summary>Longest email address accepted, as <c>EmailValidator</c> enforces.</summary>
    public const int EmailMaxLength = 254;

    /// <summary>The caller's first name. Prefills the intake form and greets them in the message.</summary>
    [Required]
    [StringLength(FirstNameMaxLength)]
    public required string FirstName { get; init; }

    /// <summary>
    /// The caller's US/Canada phone number, in any common format; normalised to E.164 before it
    /// is stored or sent. Required for a texted invite; optional otherwise, but validated when given.
    /// </summary>
    [StringLength(30)]
    public string? Phone { get; init; }

    /// <summary>
    /// The caller's email address. Required for an emailed invite; optional otherwise, but
    /// validated when given. Stored trimmed and lower-cased.
    /// </summary>
    [StringLength(EmailMaxLength)]
    public string? Email { get; init; }

    /// <summary>
    /// How to send the link: <c>sms</c> or <c>email</c> (<c>IntakeInviteChannel</c>). Omitted
    /// means <c>sms</c>, which is what every client before issue #693 meant. Ignored for self-entry.
    /// </summary>
    [StringLength(10)]
    public string? Channel { get; init; }

    /// <summary>
    /// The caller agreed to receive the link: for a text, after the advisor read the published
    /// verbal consent script; for an email, when they asked for it by email. Required for a sent
    /// invite; ignored for self-entry, which sends nothing.
    /// </summary>
    public bool ConsentCaptured { get; init; }

    /// <summary>
    /// <i>Fill it in myself</i>: mint the invite without sending it and return the prefilled intake
    /// URL for the advisor to open. Works while texting and email are disabled.
    /// </summary>
    public bool SelfEntry { get; init; }
}
