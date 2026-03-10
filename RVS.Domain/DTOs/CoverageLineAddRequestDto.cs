using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record CoverageLineAddRequestDto
    {
        [Required(ErrorMessage = "ServiceTypeCode is required.")]
        [StringLength(10, MinimumLength = 1, ErrorMessage = "ServiceTypeCode must be between 1 and 10 characters.")]
        public string ServiceTypeCode { get; init; } = default!;

        [StringLength(500, ErrorMessage = "CoverageDescription must not exceed 500 characters.")]
        public string? CoverageDescription { get; init; }

        [Range(0, 999999.99, ErrorMessage = "CopayAmount must be between 0 and 999999.99.")]
        public decimal? CopayAmount { get; init; }

        [Range(0, 100, ErrorMessage = "CoinsurancePercent must be between 0 and 100.")]
        public decimal? CoinsurancePercent { get; init; }

        [Range(0, 999999.99, ErrorMessage = "DeductibleAmount must be between 0 and 999999.99.")]
        public decimal? DeductibleAmount { get; init; }

        [Range(0, 999999.99, ErrorMessage = "RemainingDeductible must be between 0 and 999999.99.")]
        public decimal? RemainingDeductible { get; init; }

        [Range(0, 999999.99, ErrorMessage = "OutOfPocketMax must be between 0 and 999999.99.")]
        public decimal? OutOfPocketMax { get; init; }

        [Range(0, 999999.99, ErrorMessage = "RemainingOutOfPocket must be between 0 and 999999.99.")]
        public decimal? RemainingOutOfPocket { get; init; }

        [Range(0, 999999.99, ErrorMessage = "AllowanceAmount must be between 0 and 999999.99.")]
        public decimal? AllowanceAmount { get; init; }

        [StringLength(50, MinimumLength = 1, ErrorMessage = "NetworkIndicator must be between 1 and 50 characters.")]
        public string? NetworkIndicator { get; init; }

        [DataType(DataType.Date)]
        public DateTime? EffectiveDate { get; init; }

        [DataType(DataType.Date)]
        public DateTime? TerminationDate { get; init; }

        [StringLength(1000, ErrorMessage = "AdditionalInfo must not exceed 1000 characters.")]
        public string? AdditionalInfo { get; init; }
    }
}
