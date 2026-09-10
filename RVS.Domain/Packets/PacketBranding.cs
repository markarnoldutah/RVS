namespace RVS.Domain.Packets;

/// <summary>
/// The packet's displayed brand identity — the masthead title, the running/end footer, and
/// the PDF document author. Both renderers read the brand from here (via
/// <see cref="ServicePacket.Branding"/>) so there is exactly one place the displayed name
/// and logo are decided.
///
/// Today every packet uses <see cref="Default"/> ("RV Intake"). This type is the seam for
/// the future per-location override (issue <c>#470</c>): manager-configured branding on
/// <see cref="Entities.PacketConfigEmbedded"/> (<c>#435</c> — it already carries
/// <c>LogoUrl</c>) will be resolved by the packet generation orchestrator into a
/// <see cref="PacketCompositionContext"/> and flow through <see cref="PacketComposer"/>
/// into here, replacing the default. No renderer or manager UI wiring is needed for that —
/// only the orchestrator populates the context.
/// </summary>
public sealed record PacketBranding
{
    /// <summary>The product-default brand, used until a per-location override exists.</summary>
    public static readonly PacketBranding Default = new();

    /// <summary>
    /// Displayed brand name — the masthead title, the footer, and the PDF author. Defaults
    /// to the product name.
    /// </summary>
    public string BrandName { get; init; } = "RV Intake";

    /// <summary>
    /// Optional logo, as a <c>data:</c> URI (e.g. <c>data:image/png;base64,…</c>). Rendered
    /// to the left of the masthead title when present. <c>null</c> — the default — renders
    /// no logo. A data URI is used rather than a URL so neither renderer performs I/O: the
    /// HTML renderer emits it as an <c>&lt;img src&gt;</c> and the PDF renderer decodes the
    /// base64 payload to image bytes. The <c>#470</c> orchestrator will resolve a
    /// location's <c>LogoUrl</c> to this form.
    /// </summary>
    public string? LogoDataUri { get; init; }

    /// <summary><c>true</c> when a non-blank logo data URI is present.</summary>
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoDataUri);
}
