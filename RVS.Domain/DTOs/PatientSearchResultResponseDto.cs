namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Response DTO for patient search results (lightweight).
    /// </summary>
    public sealed record PatientSearchResultResponseDto
    {
        public string PatientId { get; init; } = default!;
        public string PracticeId { get; init; } = default!;
        public string FirstName { get; init; } = default!;
        public string LastName { get; init; } = default!;

        /// <summary>
        /// Patient's date of birth. Timezone-agnostic calendar date.
        /// </summary>
        public DateOnly? DateOfBirth { get; init; }

        public string? PrimaryMemberId { get; init; }
        public string? PrimaryPayerName { get; init; }
    }
}
