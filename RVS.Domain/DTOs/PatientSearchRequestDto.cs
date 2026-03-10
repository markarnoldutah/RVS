using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Patient search criteria scoped to a single practice.
    /// Practice scope is provided out-of-band (controller/service/repository parameter),
    /// never as an optional field on this DTO.
    /// </summary>
    public sealed record PatientSearchRequestDto
    {
        [StringLength(100, MinimumLength = 1, ErrorMessage = "LastName must be between 1 and 100 characters.")]
        public string? LastName { get; init; }

        [StringLength(100, MinimumLength = 1, ErrorMessage = "FirstName must be between 1 and 100 characters.")]
        public string? FirstName { get; init; }

        /// <summary>
        /// Filter by date of birth. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? DateOfBirth { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "MemberId must be between 1 and 50 characters.")]
        public string? MemberId { get; init; }

        [Range(1, int.MaxValue, ErrorMessage = "Page must be greater than 0.")]
        public int Page { get; init; } = 1;

        [Range(1, 100, ErrorMessage = "PageSize must be between 1 and 100.")]
        public int PageSize { get; init; } = 25;

        public string? ContinuationToken { get; set; }
    }
}
