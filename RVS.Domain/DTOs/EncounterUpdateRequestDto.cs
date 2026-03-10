using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Request DTO for updating an existing encounter.
    /// All fields are optional - only provided fields will be updated.
    /// </summary>
    public sealed record EncounterUpdateRequestDto
    {
        [StringLength(100, MinimumLength = 1, ErrorMessage = "LocationId must be between 1 and 100 characters.")]
        public string? LocationId { get; init; }

        /// <summary>
        /// Scheduled visit date and time. Should be provided in UTC.
        /// The API will store and return this value in UTC.
        /// </summary>
        public DateTime? VisitDate { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "VisitType must be between 1 and 50 characters.")]
        public string? VisitType { get; init; }

        /// <summary>
        /// Encounter status: scheduled, in-progress, completed, cancelled.
        /// </summary>
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Status must be between 1 and 50 characters.")]
        public string? Status { get; init; }  // scheduled, in-progress, completed, cancelled

        [StringLength(100, MinimumLength = 1, ErrorMessage = "ExternalRef must be between 1 and 100 characters.")]
        public string? ExternalRef { get; init; }
    }
}
