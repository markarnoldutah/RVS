using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Search criteria for encounters across a practice or location.
    /// </summary>
    public sealed record EncounterSearchRequestDto
    {
        [StringLength(100, MinimumLength = 1, ErrorMessage = "LocationId must be between 1 and 100 characters.")]
        public string? LocationId { get; init; }

        /// <summary>
        /// Start of date range for filtering encounters. Timezone-agnostic calendar date.
        /// Encounters with VisitDate on or after this date will be included.
        /// </summary>
        [Required(ErrorMessage = "FromDate is required.")]
        public DateOnly FromDate { get; init; }

        /// <summary>
        /// End of date range for filtering encounters. Timezone-agnostic calendar date.
        /// Encounters with VisitDate on or before this date will be included.
        /// </summary>
        [Required(ErrorMessage = "ToDate is required.")]
        public DateOnly ToDate { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "VisitType must be between 1 and 50 characters.")]
        public string? VisitType { get; init; }

        public bool? HasEligibilityCheck { get; init; }

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; init; } = 1;

        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
        public int PageSize { get; init; } = 50;
    }
}
