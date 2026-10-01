namespace RVS.Blazor.Intake;

/// <summary>
/// Who operates RV Intake, as shown on every public page (Spec A-15, issue #680).
/// These values must match the toll-free verification application (#659): the reviewer
/// checks that the company name and contact email it lists appear on <c>rvintake.com</c>.
/// </summary>
public static class SiteIdentity
{
    public const string LegalEntityName = "Arnold Digital Solutions";

    public const string ContactEmail = "support@arnolddigitalsolutions.com";

    public const string ContactMailto = "mailto:" + ContactEmail;

    /// <summary>Shown on the privacy policy and terms. Bump it whenever either page's wording changes.</summary>
    public const string PoliciesLastUpdated = "September 24, 2026";
}
