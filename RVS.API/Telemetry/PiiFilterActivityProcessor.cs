using System.Diagnostics;
using OpenTelemetry;

namespace RVS.API.Telemetry;

/// <summary>
/// OpenTelemetry activity processor that removes PII tags from telemetry before export.
/// Implements SEC-PRIV-04: No PII (email, phone, name) must appear in App Insights custom dimensions.
/// </summary>
public sealed class PiiFilterActivityProcessor : BaseProcessor<Activity>
{
    /// <summary>
    /// Tag keys that may contain PII and must be stripped from telemetry.
    /// </summary>
    internal static readonly string[] PiiTagKeys =
    [
        "email",
        "Email",
        "emailAddress",
        "EmailAddress",
        "phone",
        "Phone",
        "phoneNumber",
        "PhoneNumber",
        "firstName",
        "FirstName",
        "lastName",
        "LastName",
        "customerName",
        "CustomerName",
        "name",
        "Name"
    ];

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        foreach (var key in PiiTagKeys)
        {
            data.SetTag(key, null);
        }

        base.OnEnd(data);
    }
}
