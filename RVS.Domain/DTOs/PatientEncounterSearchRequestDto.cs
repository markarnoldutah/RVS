using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Search criteria for patient encounters scoped to a single patient.
    /// Patient, practice, and tenant scope are provided out-of-band (controller/service/repository parameters).
    /// </summary>
    public sealed record PatientEncounterSearchRequestDto
    {
        /// <summary>
        /// Start of date range for filtering encounters. Timezone-agnostic calendar date.
        /// Encounters with VisitDate on or after this date will be included.
        /// </summary>
        public DateOnly? FromDate { get; init; }

        /// <summary>
        /// End of date range for filtering encounters. Timezone-agnostic calendar date.
        /// Encounters with VisitDate on or before this date will be included.
        /// </summary>
        public DateOnly? ToDate { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "VisitType must be between 1 and 50 characters.")]
        public string? VisitType { get; init; }

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; init; } = 1;

        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
        public int PageSize { get; init; } = 20;

        public string? ContinuationToken { get; set; }
    }
}
