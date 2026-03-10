using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Request DTO for creating a new patient.
    /// </summary>
    public sealed record PatientCreateRequestDto : IValidatableObject
    {
        [Required(ErrorMessage = "FirstName is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "FirstName must be between 1 and 100 characters.")]
        public string FirstName { get; init; } = default!;

        [Required(ErrorMessage = "LastName is required.")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "LastName must be between 1 and 100 characters.")]
        public string LastName { get; init; } = default!;

        /// <summary>
        /// Patient's date of birth. This is a date-only field with no time component.
        /// Timezone-agnostic - represents the calendar date of birth.
        /// </summary>
        [Required(ErrorMessage = "DateOfBirth is required.")]
        public DateOnly DateOfBirth { get; init; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters.")]
        public string? Email { get; init; }

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
        public string? Phone { get; init; }

        /// <summary>
        /// Custom validation for DateOfBirth to ensure it's not in the future
        /// and not impossibly old (before 1900).
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            
            if (DateOfBirth > today)
            {
                yield return new ValidationResult(
                    "DateOfBirth cannot be in the future.",
                    [nameof(DateOfBirth)]);
            }

            if (DateOfBirth < new DateOnly(1900, 1, 1))
            {
                yield return new ValidationResult(
                    "DateOfBirth cannot be before 1900.",
                    [nameof(DateOfBirth)]);
            }
        }
    }
}
