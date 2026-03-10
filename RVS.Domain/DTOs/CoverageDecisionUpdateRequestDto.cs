using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record CoverageDecisionUpdateRequestDto
    {
        // TODO confirm all necessary updateable values are reflected here

        [Required(ErrorMessage = "PrimaryCoverageEnrollmentId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "PrimaryCoverageEnrollmentId must be between 1 and 100 characters.")]
        public string PrimaryCoverageEnrollmentId { get; init; } = default!;

        [StringLength(100, MinimumLength = 1, ErrorMessage = "SecondaryCoverageEnrollmentId must be between 1 and 100 characters.")]
        public string? SecondaryCoverageEnrollmentId { get; init; }

        [Required(ErrorMessage = "CobReason is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "CobReason must be between 1 and 100 characters.")]
        public string CobReason { get; init; } = default!;

        public bool OverriddenByUser { get; init; }

        [StringLength(500, ErrorMessage = "OverrideNote must not exceed 500 characters.")]
        public string? OverrideNote { get; init; }
    }
}
