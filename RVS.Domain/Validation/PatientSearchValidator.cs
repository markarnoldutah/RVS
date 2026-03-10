using System;
using System.Collections.Generic;
using System.Linq;

namespace RVS.Domain.Validation
{
    /// <summary>
    /// Validates patient search request parameters to prevent malicious input,
    /// SQL injection (though Cosmos uses parameterized queries), and excessive RU consumption.
    /// </summary>
    public static class PatientSearchValidator
    {
        // Configuration: Maximum lengths for search terms
        private const int MaxNameLength = 100;
        private const int MaxMemberIdLength = 50;
        private const int MinSearchTermLength = 1;
        private const int MaxPageSize = 100;
        private const int MaxContinuationTokenLength = 5000; // Cosmos tokens can be large

        // Dangerous patterns to block
        private static readonly char[] DangerousChars = { '<', '>', ';', '\'', '"', '\\', '\0' };
        
        /// <summary>
        /// Validates all search parameters and returns validation errors.
        /// </summary>
        public static ValidationResult Validate(
            string? lastName,
            string? firstName,
            string? memberId,
            int pageSize,
            string? continuationToken)
        {
            var errors = new List<string>();

            // Validate LastName
            if (!string.IsNullOrWhiteSpace(lastName))
            {
                if (lastName.Length < MinSearchTermLength)
                    errors.Add($"LastName must be at least {MinSearchTermLength} character(s).");
                
                if (lastName.Length > MaxNameLength)
                    errors.Add($"LastName cannot exceed {MaxNameLength} characters.");
                
                if (ContainsDangerousCharacters(lastName))
                    errors.Add("LastName contains invalid characters.");
                
                if (IsOnlyWhitespace(lastName))
                    errors.Add("LastName cannot contain only whitespace.");
            }

            // Validate FirstName
            if (!string.IsNullOrWhiteSpace(firstName))
            {
                if (firstName.Length < MinSearchTermLength)
                    errors.Add($"FirstName must be at least {MinSearchTermLength} character(s).");
                
                if (firstName.Length > MaxNameLength)
                    errors.Add($"FirstName cannot exceed {MaxNameLength} characters.");
                
                if (ContainsDangerousCharacters(firstName))
                    errors.Add("FirstName contains invalid characters.");
                
                if (IsOnlyWhitespace(firstName))
                    errors.Add("FirstName cannot contain only whitespace.");
            }

            // Validate MemberId
            if (!string.IsNullOrWhiteSpace(memberId))
            {
                if (memberId.Length < MinSearchTermLength)
                    errors.Add($"MemberId must be at least {MinSearchTermLength} character(s).");
                
                if (memberId.Length > MaxMemberIdLength)
                    errors.Add($"MemberId cannot exceed {MaxMemberIdLength} characters.");
                
                if (ContainsDangerousCharacters(memberId))
                    errors.Add("MemberId contains invalid characters.");
                
                if (IsOnlyWhitespace(memberId))
                    errors.Add("MemberId cannot contain only whitespace.");
            }

            // Validate PageSize
            if (pageSize <= 0)
                errors.Add("PageSize must be greater than 0.");
            
            if (pageSize > MaxPageSize)
                errors.Add($"PageSize cannot exceed {MaxPageSize}.");

            // Validate ContinuationToken (basic length check)
            if (!string.IsNullOrWhiteSpace(continuationToken) && 
                continuationToken.Length > MaxContinuationTokenLength)
            {
                errors.Add($"ContinuationToken exceeds maximum length of {MaxContinuationTokenLength} characters.");
            }

            // Require at least one search criterion
            if (string.IsNullOrWhiteSpace(lastName) &&
                string.IsNullOrWhiteSpace(firstName) &&
                string.IsNullOrWhiteSpace(memberId))
            {
                errors.Add("At least one search criterion (LastName, FirstName, or MemberId) must be provided.");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        /// <summary>
        /// Sanitizes a search term by trimming and normalizing whitespace.
        /// Call this AFTER validation passes.
        /// </summary>
        public static string SanitizeSearchTerm(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return string.Empty;

            // Trim leading/trailing whitespace
            // Normalize internal whitespace to single spaces
            return string.Join(" ", 
                term.Split(new[] { ' ', '\t', '\n', '\r' }, 
                    StringSplitOptions.RemoveEmptyEntries));
        }

        private static bool ContainsDangerousCharacters(string input)
        {
            return input.IndexOfAny(DangerousChars) >= 0;
        }

        private static bool IsOnlyWhitespace(string input)
        {
            return input.All(char.IsWhiteSpace);
        }
    }

    /// <summary>
    /// Validation result with detailed error messages.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public List<string> Errors { get; init; } = new();
        
        public string GetErrorMessage() => string.Join(" ", Errors);
    }
}
