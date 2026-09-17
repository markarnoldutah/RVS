namespace RVS.Domain.DTOs;

/// <summary>
/// Outcome of one <c>go.rvintake.com</c> redirect (<c>Spec A-13</c>, issue #599).
/// </summary>
/// <param name="TargetUrl">Absolute intake URL to send the customer to. Always populated.</param>
/// <param name="Source">The channel tag recorded for the hit, already normalised.</param>
/// <param name="SlugResolved">
/// Whether the slug resolved to a location. <c>false</c> still redirects — the intake app owns
/// the "no such location" page, and failing here would turn a typo into a dead link.
/// </param>
public sealed record IntakeRedirectResultDto(string TargetUrl, string Source, bool SlugResolved);
