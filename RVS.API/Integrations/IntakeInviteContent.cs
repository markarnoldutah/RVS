using System.Net;

namespace RVS.API.Integrations;

/// <summary>
/// Text of an advisor intake invite (<c>Spec A-14</c>, issue #663): who it is from, a greeting,
/// the link, and the compliance tail. Laddered like <see cref="ServiceRequestConfirmationContent"/>:
/// under length pressure it drops the greeting, then the location name, then the compliance tail,
/// and never shortens the link, which is the whole message.
///
/// The emailed invite (issue #693) has no length pressure and no carrier keywords, so it carries
/// neither the ladder nor the STOP/HELP tail. It says instead that the link is single use and
/// when it stops working. Everything the advisor typed is HTML-encoded in the HTML body.
/// </summary>
internal static class IntakeInviteContent
{
    /// <summary>
    /// Two concatenated GSM-7 segments (2 × 153). The invite link alone is about 100 characters,
    /// so one segment would lose the sender and the compliance tail on almost every send, and a
    /// first text to a new number needs both.
    /// </summary>
    public const int SmsMaxLength = 306;

    /// <summary>
    /// Submitted verbatim as a sample message in the toll-free verification application (#659),
    /// so it changes only in step with that application and with <c>Spec A-2</c>'s disclosure
    /// wording. <c>HELP</c> is answered by the inbound keyword handler (#665).
    /// </summary>
    private const string Compliance = "Msg & data rates may apply. Reply STOP to opt out, HELP for help.";

    public static string BuildSmsBody(string locationName, string firstName, string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        string[] ladder =
        [
            $"{locationName}: Hi {firstName}, here's the link to start your service request: {link} {Compliance}",
            $"{locationName}: Here's the link to start your service request: {link} {Compliance}",
            $"{link} {Compliance}",
        ];

        return ladder.FirstOrDefault(body => body.Length <= SmsMaxLength) ?? link;
    }

    public static string BuildEmailSubject(string locationName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);

        return $"Your service request link from {locationName}";
    }

    public static string BuildEmailHtmlBody(string locationName, string firstName, string link, int expiryHours)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        var location = WebUtility.HtmlEncode(locationName);
        var name = WebUtility.HtmlEncode(firstName);
        var href = WebUtility.HtmlEncode(link);

        return
            $"<p>Hi {name},</p>" +
            $"<p>Here's the link to start your service request with <strong>{location}</strong>. " +
            "You can describe the problem and add photos of it.</p>" +
            $"<p><a href=\"{href}\">Start your service request</a></p>" +
            $"<p>Or paste this into your browser: {href}</p>" +
            $"<p>The link works once, for the next {expiryHours} hours. " +
            $"You're getting this email because you asked {location} to send it.</p>";
    }

    public static string BuildEmailPlainTextBody(string locationName, string firstName, string link, int expiryHours)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        return
            $"Hi {firstName},\n\n" +
            $"Here's the link to start your service request with {locationName}. " +
            "You can describe the problem and add photos of it.\n\n" +
            $"{link}\n\n" +
            $"The link works once, for the next {expiryHours} hours. " +
            $"You're getting this email because you asked {locationName} to send it.\n";
    }
}
