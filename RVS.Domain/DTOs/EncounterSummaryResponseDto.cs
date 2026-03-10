namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Summary response DTO for encounter listings.
    /// </summary>
    public record EncounterSummaryResponseDto
    {
        public string EncounterId { get; init; } = default!;
        public string PatientId { get; init; } = default!;
        public string PracticeId { get; init; } = default!;
        public string LocationId { get; init; } = default!;

        /// <summary>
        /// Scheduled visit date and time in UTC.
        /// </summary>
        public DateTime VisitDate { get; init; }

        public string VisitType { get; init; } = default!;
        public string Status { get; init; } = default!;
        public bool HasEligibilityChecks { get; init; }
        public string? PrimaryPayerName { get; init; }
        public string? CobSummary { get; init; }

        /// <summary>
        /// Timestamp when the encounter was created. Always in UTC.
        /// </summary>
        public DateTime CreatedAtUtc { get; init; }

        /// <summary>
        /// Timestamp when the encounter was last updated. Always in UTC. Null if never updated.
        /// </summary>
        public DateTime? UpdatedAtUtc { get; init; }

        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
