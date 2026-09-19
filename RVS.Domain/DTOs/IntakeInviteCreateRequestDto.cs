using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs;

/// <summary>
/// Request to create an advisor intake invite (<c>Spec A-14</c>, issue #663): text a prefilled,
/// single-use intake link to a caller, or, with <see cref="SelfEntry"/>, mint one for the
/// advisor to fill in during the call.
/// </summary>
public sealed record IntakeInviteCreateRequestDto
{
    /// <summary>Longest first name accepted.</summary>
    public const int FirstNameMaxLength = 50;

    /// <summary>The caller's first name. Prefills the intake form and greets them in the text.</summary>
    [Required]
    [StringLength(FirstNameMaxLength)]
    public required string FirstName { get; init; }

    /// <summary>
    /// The caller's US/Canada phone number, in any common format; normalised to E.164 before it
    /// is stored or sent. Required unless <see cref="SelfEntry"/> is set.
    /// </summary>
    [StringLength(30)]
    public string? Phone { get; init; }

    /// <summary>
    /// The advisor read the published verbal consent script and the caller agreed to receive the
    /// text. Required for a texted invite; ignored for self-entry, which texts nobody.
    /// </summary>
    public bool ConsentCaptured { get; init; }

    /// <summary>
    /// <i>Fill it in myself</i>: mint the invite without texting it and return the prefilled intake
    /// URL for the advisor to open. Works while texting is disabled.
    /// </summary>
    public bool SelfEntry { get; init; }
}
