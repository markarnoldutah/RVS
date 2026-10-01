namespace RVS.Domain.Packets;

/// <summary>
/// The packet's displayed brand identity — the masthead title and logo, the running/end footer,
/// the "Powered by" mark, and the PDF document author. Both renderers read the brand from here
/// (via <see cref="ServicePacket.Branding"/>) so there is exactly one place the displayed name
/// and logos are decided.
///
/// The dealer's logo comes from the location's branding (<c>Spec A-16</c>, issue <c>#470</c>):
/// <c>PacketGenerationService</c> fetches it, and only a logo that fetched as a real image reaches
/// <see cref="PacketCompositionContext.LogoUrl"/>, so the HTML and the PDF show it or both omit it.
/// </summary>
public sealed record PacketBranding
{
    /// <summary>The product name, which the "Powered by" mark always carries.</summary>
    public const string ProductName = "RV Intake";

    /// <summary>
    /// Where the Intake app serves the RV Intake horizontal lockup as a PNG, relative to its
    /// origin. Mail clients render neither SVG nor <c>data:</c> images, so the packet email
    /// needs a hosted raster copy of the kit's <c>logo-horizontal.svg</c>.
    /// </summary>
    public const string PoweredByLogoPath = "_content/RVS.UI.Shared/brand/logo-horizontal.png";

    /// <summary>The product-default brand, used when a location sets no logo.</summary>
    public static readonly PacketBranding Default = new();

    /// <summary>
    /// Displayed brand name — the masthead title, the footer, and the PDF author. Defaults
    /// to the product name.
    /// </summary>
    public string BrandName { get; init; } = ProductName;

    /// <summary>
    /// The dealer's logo as an absolute https URL, shown top left in the masthead, or
    /// <c>null</c> for none. The HTML references it by URL rather than embedding it, because
    /// the HTML is the email body and Gmail and Outlook drop <c>data:</c> images; the PDF
    /// renderer is handed the same image's bytes by its caller.
    /// </summary>
    public string? LogoUrl { get; init; }

    /// <summary>
    /// Absolute URL of the RV Intake mark for the HTML footer's "Powered by" line, or
    /// <c>null</c> to name the product in text instead. Built from the Intake app's origin with
    /// <see cref="PoweredByLogoUrlFor"/>.
    /// </summary>
    public string? PoweredByLogoUrl { get; init; }

    /// <summary><c>true</c> when a dealer logo is present.</summary>
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoUrl);

    /// <summary>
    /// The "Powered by" mark's URL under <paramref name="intakeBaseUrl"/>, or <c>null</c> when
    /// the origin is blank or not https.
    /// </summary>
    /// <param name="intakeBaseUrl">The Intake app's origin, e.g. <c>https://rvintake.com</c>.</param>
    public static string? PoweredByLogoUrlFor(string? intakeBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(intakeBaseUrl)
            || !intakeBaseUrl.Trim().StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"{intakeBaseUrl.Trim().TrimEnd('/')}/{PoweredByLogoPath}";
    }
}
