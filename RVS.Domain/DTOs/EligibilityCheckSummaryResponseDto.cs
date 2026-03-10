namespace RVS.Domain.DTOs
{
    public record EligibilityCheckSummaryResponseDto
    {
        public string EligibilityCheckId { get; init; } = default!;
        public string CoverageEnrollmentId { get; init; } = default!;
        public string PayerId { get; init; } = default!;
        public DateTime DateOfService { get; init; }
        public DateTime RequestedAtUtc { get; init; }
        public DateTime? CompletedAtUtc { get; init; }
        public string Status { get; init; } = default!;
        
        /// <summary>
        /// The earliest UTC time when the next poll should be performed.
        /// Only relevant when Status is "InProgress" or "Pending".
        /// 
        /// Polling Logic:
        /// - If null: Poll immediately (no time restriction)
        /// - If value &lt;= DateTime.UtcNow: Ready to poll (the wait time has passed)
        /// - If value &gt; DateTime.UtcNow: Wait before polling (not ready yet)
        /// 
        /// Example: If NextPollAfterUtc = 10:30:00 UTC
        ///   - At 10:29:00 UTC: DON'T poll yet (NextPollAfterUtc > Now)
        ///   - At 10:30:01 UTC: POLL now (NextPollAfterUtc &lt;= Now)
        /// 
        /// This prevents over-polling Availity and optimizes RU costs by respecting
        /// the recommended wait time between poll attempts.
        /// </summary>
        public DateTime? NextPollAfterUtc { get; init; }
        
        /// <summary>
        /// Number of times this check has been polled from Availity.
        /// Used to track retry attempts and enforce maximum poll limits.
        /// </summary>
        public int PollCount { get; init; }
        
        public string? PayerName { get; init; }
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
