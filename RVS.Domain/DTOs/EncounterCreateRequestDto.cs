using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// DTO for creating a new encounter.
    /// Note: PatientId is no longer in the DTO since it's provided via the route.
    /// </summary>
    public sealed record EncounterCreateRequestDto
    {
        [Required(ErrorMessage = "LocationId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "LocationId must be between 1 and 100 characters.")]
        public string LocationId { get; init; } = default!;

        /// <summary>
        /// Scheduled visit date and time. Should be provided in UTC.
        /// The API will store and return this value in UTC.
        /// </summary>
        [Required(ErrorMessage = "VisitDate is required.")]
        public DateTime VisitDate { get; init; }

        [Required(ErrorMessage = "VisitType is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "VisitType must be between 1 and 50 characters.")]
        public string VisitType { get; init; } = default!;

        [StringLength(100, MinimumLength = 1, ErrorMessage = "ExternalRef must be between 1 and 100 characters.")]
        public string? ExternalRef { get; init; }
    }
}
