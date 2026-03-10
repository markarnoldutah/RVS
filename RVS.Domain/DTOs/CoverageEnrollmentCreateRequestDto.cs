using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Request DTO for creating a new coverage enrollment.
    /// </summary>
    public sealed record CoverageEnrollmentCreateRequestDto
    {
        [Required(ErrorMessage = "PayerId is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "PayerId must be between 1 and 100 characters.")]
        public string PayerId { get; init; } = default!;

        [Required(ErrorMessage = "PlanType is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "PlanType must be between 1 and 50 characters.")]
        public string PlanType { get; init; } = default!;

        [Required(ErrorMessage = "MemberId is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "MemberId must be between 1 and 50 characters.")]
        public string MemberId { get; init; } = default!;

        [StringLength(50, MinimumLength = 1, ErrorMessage = "GroupNumber must be between 1 and 50 characters.")]
        public string? GroupNumber { get; init; }

        [Required(ErrorMessage = "RelationshipToSubscriber is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "RelationshipToSubscriber must be between 1 and 50 characters.")]
        public string RelationshipToSubscriber { get; init; } = default!;

        [StringLength(100, MinimumLength = 1, ErrorMessage = "SubscriberFirstName must be between 1 and 100 characters.")]
        public string? SubscriberFirstName { get; init; }

        [StringLength(100, MinimumLength = 1, ErrorMessage = "SubscriberLastName must be between 1 and 100 characters.")]
        public string? SubscriberLastName { get; init; }

        /// <summary>
        /// Subscriber's date of birth. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? SubscriberDob { get; init; }

        public bool IsEmployerPlan { get; init; }

        /// <summary>
        /// Coverage effective date. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? EffectiveDate { get; init; }

        /// <summary>
        /// Coverage termination date. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? TerminationDate { get; init; }

        [Range(1, 99, ErrorMessage = "CobPriorityHint must be between 1 and 99.")]
        public byte? CobPriorityHint { get; init; }

        [StringLength(1000, ErrorMessage = "CobNotes must not exceed 1000 characters.")]
        public string? CobNotes { get; init; }
    }
}
