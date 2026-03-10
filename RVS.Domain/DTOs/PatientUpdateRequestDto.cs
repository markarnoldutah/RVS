using System.ComponentModel.DataAnnotations;

namespace RVS.Domain.DTOs
{
    /// <summary>
    /// Request DTO for updating an existing patient.
    /// All fields are optional - only provided fields will be updated.
    /// </summary>
    public sealed record PatientUpdateRequestDto
    {
        [StringLength(100, MinimumLength = 1, ErrorMessage = "FirstName must be between 1 and 100 characters.")]
        public string? FirstName { get; init; }

        [StringLength(100, MinimumLength = 1, ErrorMessage = "LastName must be between 1 and 100 characters.")]
        public string? LastName { get; init; }

        /// <summary>
        /// Patient's date of birth. This is a date-only field with no time component.
        /// Timezone-agnostic - represents the calendar date of birth.
        /// </summary>
        public DateOnly? DateOfBirth { get; init; }

        [EmailAddress(ErrorMessage = "Invalid email address format.")]
        [StringLength(255, ErrorMessage = "Email must not exceed 255 characters.")]
        public string? Email { get; init; }

        [Phone(ErrorMessage = "Invalid phone number format.")]
        [StringLength(20, ErrorMessage = "Phone must not exceed 20 characters.")]
        public string? Phone { get; init; }
    }
}
