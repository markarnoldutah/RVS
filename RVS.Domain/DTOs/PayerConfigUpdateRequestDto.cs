using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record PayerConfigUpdateRequestDto
    {
        [StringLength(200, MinimumLength = 1, ErrorMessage = "DisplayNameOverride must be between 1 and 200 characters.")]
        public string? DisplayNameOverride { get; init; }

        public bool? IsEnabled { get; init; }

        [StringLength(1000, ErrorMessage = "Notes must not exceed 1000 characters.")]
        public string? Notes { get; init; }
    }
}
