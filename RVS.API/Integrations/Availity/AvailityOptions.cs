using System.ComponentModel.DataAnnotations;

namespace RVS.API.Integrations.Availity;

public sealed class AvailityOptions
{
    [Required]
    public string BaseUrl { get; init; } = "https://api.availity.com";

    /// <summary>
    /// If using OAuth2 client credentials, store the token endpoint here.
    /// Leave null if an upstream handler injects auth.
    /// </summary>
    public string? TokenUrl { get; init; }

    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Optional bearer token for dev/test. In production use OAuth2.
    /// </summary>
    public string? StaticBearerToken { get; init; }

    /// <summary>
    /// The relative path for the eligibility endpoint (product-specific).
    /// </summary>
    public string EligibilityPath { get; init; } = "/eligibility";
}
