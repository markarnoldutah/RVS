using System.Text;

namespace RVS.Blazor.Intake.Pages;

/// <summary>
/// Pure presentation helpers for the customer status page (<c>Spec X-1</c>): turning a
/// stored service-request status into a short customer-facing sentence, and a free-form
/// location phone string into a display form and a <c>tel:</c> dial target.
/// No customer identity, issue text, or conversation is involved — this is formatting only.
/// </summary>
public static class CustomerStatusFormatting
{
    private const string GenericStatusDescription = "Your request is being processed.";

    /// <summary>
    /// Maps a stored status value to a one-line customer-facing description. Recognises the
    /// decided vocabulary (<c>Spec C-3</c> / C-8): <c>New</c>, <c>InProgress</c>,
    /// <c>WaitingOnParts</c>, <c>WaitingOnCustomer</c>, <c>Completed</c>, <c>Cancelled</c>
    /// (spacing and hyphens are ignored, so legacy "In Progress" style values still match).
    /// Anything unrecognised, null, or blank gets a safe generic line.
    /// </summary>
    public static string DescribeStatus(string? status) => Canonicalize(status) switch
    {
        "new" => "Your request has been received and is awaiting review by a service advisor.",
        "inprogress" => "A technician is actively working on your service request.",
        "waitingonparts" => "Your repair requires parts that have been ordered. We'll update you when they arrive.",
        "waitingoncustomer" => "We're waiting on information or approval from you before work can continue. Please contact the dealership.",
        "completed" => "Your service has been completed. Please contact the dealership for pickup details.",
        "cancelled" => "This service request has been cancelled.",
        _ => GenericStatusDescription
    };

    /// <summary>
    /// Formats a location phone string for display. A 10-digit North-American number becomes
    /// <c>(NXX) NXX-XXXX</c>; an 11-digit number starting with <c>1</c> becomes
    /// <c>+1 (NXX) NXX-XXXX</c>. Anything else is returned trimmed and otherwise unchanged.
    /// Returns <c>null</c> when there is no usable value.
    /// </summary>
    public static string? FormatPhoneForDisplay(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var digits = DigitsOnly(trimmed);

        return digits.Length switch
        {
            10 => $"({digits[..3]}) {digits[3..6]}-{digits[6..]}",
            11 when digits[0] == '1' => $"+1 ({digits[1..4]}) {digits[4..7]}-{digits[7..]}",
            _ => trimmed
        };
    }

    /// <summary>
    /// Builds the <c>tel:</c> URI for a phone string. A leading <c>+</c> is preserved; a bare
    /// 10-digit number is assumed North-American and gets <c>+1</c>; an 11-digit number
    /// starting with <c>1</c> gets a <c>+</c>. Other digit strings are dialled as-is.
    /// Returns <c>null</c> when the input contains no digits.
    /// </summary>
    public static string? ToTelHref(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var trimmed = phone.Trim();
        var digits = DigitsOnly(trimmed);

        if (digits.Length == 0)
        {
            return null;
        }

        if (trimmed.StartsWith('+'))
        {
            return $"tel:+{digits}";
        }

        return digits.Length switch
        {
            10 => $"tel:+1{digits}",
            11 when digits[0] == '1' => $"tel:+{digits}",
            _ => $"tel:{digits}"
        };
    }

    private static string Canonicalize(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(status.Length);
        foreach (var ch in status)
        {
            if (ch is ' ' or '-' or '_')
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString();
    }

    private static string DigitsOnly(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}
