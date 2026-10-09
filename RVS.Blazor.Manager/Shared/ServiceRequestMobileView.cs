using System.Text;
using RVS.Domain.DTOs;
using RVS.Domain.Validation;

namespace RVS.Blazor.Manager.Shared;

/// <summary>
/// Pure logic behind the mobile request page the packet email links to (<c>Spec C-7</c>,
/// issue #743): the C-3 status set with its labels, the update request a status change sends,
/// the call / text / email links for the customer, and the header lines.
/// </summary>
public static class ServiceRequestMobileView
{
    /// <summary>The C-3 status set, in path order, with the Spec's display labels.</summary>
    public static IReadOnlyList<StatusOption> Statuses { get; } =
    [
        new("New", "New"),
        new("InProgress", "In Progress"),
        new("WaitingOnParts", "Waiting on Parts"),
        new("WaitingOnCustomer", "Waiting on Customer"),
        new("Completed", "Completed"),
        new("Cancelled", "Cancelled"),
    ];

    /// <summary>Display label for a stored status; an unknown value is echoed back.</summary>
    public static string GetStatusLabel(string status) =>
        Statuses.FirstOrDefault(s => string.Equals(s.Status, status, StringComparison.OrdinalIgnoreCase))?.Label ?? status;

    /// <summary>
    /// The update request that leaves the service request as loaded, except for the activity
    /// log. <c>ApplyUpdate</c> is a full overwrite — every field absent from the request is
    /// written back as null — so each overwritten field is carried forward. Customer and asset
    /// stay null, which the API reads as "unchanged". Callers override with <c>with { … }</c>.
    /// </summary>
    public static ServiceRequestUpdateRequestDto BuildUpdateRequest(ServiceRequestDetailResponseDto sr, string? technicianSummary)
    {
        ArgumentNullException.ThrowIfNull(sr);

        return new ServiceRequestUpdateRequestDto
        {
            Status = sr.Status,
            IssueDescription = sr.IssueDescription,
            IssueCategory = sr.IssueCategory,
            TechnicianSummary = technicianSummary,
            Priority = sr.Priority ?? "Medium",
            Urgency = sr.Urgency,
            RvUsage = sr.RvUsage,
            HasExtendedWarranty = sr.HasExtendedWarranty,
            ApproxPurchaseDate = sr.ApproxPurchaseDate,
            AssignedTechnicianId = sr.AssignedTechnicianId,
            ScheduledDateUtc = sr.ScheduledDateUtc,
            RequiredSkills = [.. sr.RequiredSkills],
            BoardSequence = sr.BoardSequence,
            JobType = sr.JobType,
        };
    }

    /// <summary>
    /// Call, text and email links for the customer, in that order, with the customer's preferred
    /// method (Spec A-2) marked. A US/CA number is dialled in E.164; any other number is dialled
    /// as its digits (and a leading <c>+</c>) rather than dropped, since the manager may still
    /// want to call it. A phone with no digits offers neither call nor text.
    /// </summary>
    public static IReadOnlyList<ContactLink> BuildContactLinks(CustomerInfoDto customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        var preferred = PreferredContactMethod.Normalize(customer.PreferredContact);
        var links = new List<ContactLink>(3);

        var dialable = ToDialable(customer.Phone);
        if (dialable is not null)
        {
            links.Add(new(PreferredContactMethod.Phone, "Call", $"tel:{dialable}", preferred == PreferredContactMethod.Phone));
            links.Add(new(PreferredContactMethod.Text, "Text", $"sms:{dialable}", preferred == PreferredContactMethod.Text));
        }

        if (!string.IsNullOrWhiteSpace(customer.Email))
        {
            var email = customer.Email.Trim();
            var at = email.LastIndexOf('@');
            var href = at > 0
                ? $"mailto:{Uri.EscapeDataString(email[..at])}@{Uri.EscapeDataString(email[(at + 1)..])}"
                : $"mailto:{Uri.EscapeDataString(email)}";
            links.Add(new(PreferredContactMethod.Email, "Email", href, preferred == PreferredContactMethod.Email));
        }

        return links;
    }

    /// <summary>
    /// The short reference code the packet prints (Spec B-2 item 3): the first hyphen-delimited
    /// segment of the id, upper-cased.
    /// </summary>
    public static string GetReferenceCode(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        return id.Split('-', 2)[0].ToUpperInvariant();
    }

    /// <summary>"2021 Grand Design Momentum", or <c>null</c> when none of year, make or model is known.</summary>
    public static string? GetUnitLine(AssetInfoDto? asset)
    {
        if (asset is null) return null;

        var parts = new[] { asset.Year?.ToString(System.Globalization.CultureInfo.InvariantCulture), asset.Manufacturer, asset.Model }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToArray();

        return parts.Length == 0 ? null : string.Join(' ', parts);
    }

    /// <summary>The customer's first and last name.</summary>
    public static string GetCustomerName(CustomerInfoDto customer)
    {
        ArgumentNullException.ThrowIfNull(customer);

        return $"{customer.FirstName?.Trim()} {customer.LastName?.Trim()}".Trim();
    }

    private static string? ToDialable(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;

        var e164 = PhoneNumberNormalizer.Normalize(phone);
        if (e164 is not null) return e164;

        var trimmed = phone.Trim();
        var sb = new StringBuilder(trimmed.Length);
        if (trimmed.StartsWith('+')) sb.Append('+');
        foreach (var ch in trimmed.Where(char.IsAsciiDigit)) sb.Append(ch);

        return sb.Length == 0 || sb.ToString() == "+" ? null : sb.ToString();
    }
}

/// <summary>One C-3 status: the stored value and its display label.</summary>
public sealed record StatusOption(string Status, string Label);

/// <summary>One way to reach the customer from the mobile page.</summary>
/// <param name="Method">The <c>PreferredContactMethod</c> value: <c>Phone</c>, <c>Text</c> or <c>Email</c>.</param>
/// <param name="Label">Button label: Call, Text or Email.</param>
/// <param name="Href">A <c>tel:</c>, <c>sms:</c> or <c>mailto:</c> URL.</param>
/// <param name="IsPreferred">True for the method the customer chose at intake.</param>
public sealed record ContactLink(string Method, string Label, string Href, bool IsPreferred);
