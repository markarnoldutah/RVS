namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Detailed response DTO for a patient including coverage enrollments and recent encounters.
    /// </summary>
    public sealed record PatientDetailResponseDto
    {
        public string PatientId { get; init; } = default!;
        public string TenantId { get; init; } = default!;
        public string PracticeId { get; init; } = default!;
        public string FirstName { get; init; } = default!;
        public string LastName { get; init; } = default!;

        /// <summary>
        /// Patient's date of birth. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? DateOfBirth { get; init; }

        public string? Email { get; init; }
        public string? Phone { get; init; }

        public List<CoverageEnrollmentResponseDto> CoverageEnrollments { get; init; } = new();

        // TODO review RecentEncounters calculated property
        public List<EncounterSummaryResponseDto> RecentEncounters { get; init; } = new();

        /// <summary>
        /// Timestamp when the patient record was created. Always in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; }

        /// <summary>
        /// Timestamp when the patient record was last updated. Always in UTC. Null if never updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; init; }

        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
