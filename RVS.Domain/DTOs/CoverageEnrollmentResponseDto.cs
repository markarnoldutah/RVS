namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Response DTO for coverage enrollment details.
    /// </summary>
    public sealed record CoverageEnrollmentResponseDto
    {
        public string CoverageEnrollmentId { get; init; } = default!;
        public string PayerId { get; init; } = default!;
        public string PlanType { get; init; } = default!;
        public string MemberId { get; init; } = default!;
        public string? GroupNumber { get; init; }
        public string RelationshipToSubscriber { get; init; } = default!;
        public string? SubscriberFirstName { get; init; }
        public string? SubscriberLastName { get; init; }

        /// <summary>
        /// Subscriber's date of birth. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? SubscriberDob { get; init; }

        public bool IsEmployerPlan { get; init; }
        
        /// <summary>
        /// Convenience property - true if PlanType is "Vision".
        /// </summary>
        public bool IsVisionPlan => PlanType?.Equals("Vision", StringComparison.OrdinalIgnoreCase) ?? false;
        
        /// <summary>
        /// Convenience property - true if PlanType is "Medical".
        /// </summary>
        public bool IsMedicalPlan => PlanType?.Equals("Medical", StringComparison.OrdinalIgnoreCase) ?? false;

        /// <summary>
        /// Coverage effective date. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? EffectiveDate { get; init; }

        /// <summary>
        /// Coverage termination date. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? TerminationDate { get; init; }

        public bool IsEnabled { get; init; }
        public byte? CobPriorityHint { get; init; }
        public bool IsCobLocked { get; init; }
        public string? CobNotes { get; init; }

        /// <summary>
        /// Timestamp when the enrollment was created. Always in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; }

        /// <summary>
        /// Timestamp when the enrollment was last updated. Always in UTC. Null if never updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; init; }

        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
