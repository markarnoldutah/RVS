using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    public sealed record EligibilityPayloadAddRequestDto
    {
        [Required(ErrorMessage = "Direction is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Direction must be between 1 and 50 characters.")]
        public string Direction { get; init; } = default!;  // Request, Response

        [Required(ErrorMessage = "Format is required.")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "Format must be between 1 and 50 characters.")]
        public string Format { get; init; } = default!;     // X12_270, X12_271, JSON

        [Required(ErrorMessage = "StorageUrl is required.")]
        [StringLength(2000, MinimumLength = 1, ErrorMessage = "StorageUrl must be between 1 and 2000 characters.")]
        [Url(ErrorMessage = "StorageUrl must be a valid URL.")]
        public string StorageUrl { get; init; } = default!;
    }
}
