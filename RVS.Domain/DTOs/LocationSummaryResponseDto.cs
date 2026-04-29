namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Summary projection of a <see cref="Entities.Location"/> — contains the
    /// identifying info, contact basics, and capability badges needed to render
    /// a location in a list/table without making a per-row detail fetch.
    /// </summary>
    public sealed record LocationSummaryResponseDto
    {
        public string LocationId { get; init; } = default!;
        public string Name { get; init; } = default!;

        /// <summary>URL-safe slug for the location's intake form path.</summary>
        public string Slug { get; init; } = string.Empty;

        /// <summary>Optional public-facing phone number.</summary>
        public string? Phone { get; init; }

        /// <summary>Optional postal address (omitted when no address fields are populated).</summary>
        public AddressDto? Address { get; init; }

        /// <summary>Capability codes enabled for this location (used as table badges).</summary>
        public List<string> EnabledCapabilities { get; init; } = [];

        /// <summary>Creation timestamp (UTC).</summary>
        public DateTime CreatedAtUtc { get; init; }
    }
}
