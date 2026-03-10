using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record PracticeDetailResponseDto : PracticeSummaryResponseDto
    {
        public string? Phone { get; init; }
        
        [EmailAddress(ErrorMessage = "Invalid email address format")]
        public string? Email { get; init; }

        // Audit properties
        public DateTime CreatedAtUtc { get; init; }
        public DateTime? UpdatedAtUtc { get; init; }
        public string? CreatedByUserId { get; init; }
        public string? UpdatedByUserId { get; init; }
    }
}
