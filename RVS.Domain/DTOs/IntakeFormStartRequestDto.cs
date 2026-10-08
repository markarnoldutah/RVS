namespace RVS.Domain.DTOs;

/// <summary>
/// Body of <c>POST api/intake/{locationSlug}/starts</c> (<c>Spec A-13</c>, issue #839): the
/// intake app reporting that a visit reached Step 1. Both fields are optional on the wire — the
/// endpoint answers <c>204</c> whatever arrives, and a row is written only when it makes sense.
/// </summary>
/// <param name="SessionId">The intake app's per-tab visit id. A missing or malformed one is not recorded.</param>
/// <param name="Src">Raw <c>src</c> the visit arrived with, or <c>null</c> for print.</param>
public sealed record IntakeFormStartRequestDto(string? SessionId, string? Src);
