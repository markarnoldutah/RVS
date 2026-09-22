namespace RVS.UI.Shared.Components;

/// <summary>The lockups from the RV Intake logo kit that <c>BrandWordmark</c> can draw.</summary>
public enum BrandWordmarkVariant
{
    /// <summary>Badge plus "RV Intake" — the default app-bar lockup.</summary>
    Horizontal,

    /// <summary>The square badge alone, for narrow chrome.</summary>
    Icon,

    /// <summary>"RV Intake" with no badge, for headers where a square badge does not fit.</summary>
    TextOnly
}
