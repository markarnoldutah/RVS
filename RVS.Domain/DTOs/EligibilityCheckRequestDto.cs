using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record EligibilityCheckRequestDto
    {
        [Required(ErrorMessage = "CoverageEnrollmentId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "CoverageEnrollmentId must be between 1 and 100 characters.")]
        public string CoverageEnrollmentId { get; init; } = default!;

        [DataType(DataType.Date)]
        public DateTime? OverrideDateOfService { get; init; }

        public List<string>? ServiceTypeCodes { get; init; }
    }
}
