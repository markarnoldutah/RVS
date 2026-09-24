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

    /// <remarks>
    /// Wording from issue #710: the customer asked for the link during a call, so the email says
    /// so, says it comes from RV Intake on the dealer's behalf, and sends questions back to the
    /// location's phone. A location with no phone on file still gets the dealer's name.
    /// </remarks>
    public static string BuildEmailHtmlBody(
        string locationName, string firstName, string link, int expiryHours, string? locationPhone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        var location = WebUtility.HtmlEncode(locationName);
        var name = WebUtility.HtmlEncode(firstName);
        var href = WebUtility.HtmlEncode(link);
        var phone = string.IsNullOrWhiteSpace(locationPhone) ? null : WebUtility.HtmlEncode(locationPhone.Trim());

        return
            $"<p>Hi {name},</p>" +
            $"<p>You've initiated a service request for your RV with <strong>{location}</strong>. " +
            "Use the link below to provide the service manager with important details about your issue:</p>" +
            $"<p><a href=\"{href}\">Start your service request</a> with {location}.</p>" +
            $"<p>Or paste this into your browser: {href}</p>" +
            $"<p>You're receiving this email from RV Intake on behalf of {location} because you initiated a service request. " +
            $"This link will work once and expires in {expiryHours} hours.</p>" +
            $"<p>{QuestionsLine(location, phone)}</p>";
    }

    public static string BuildEmailPlainTextBody(
        string locationName, string firstName, string link, int expiryHours, string? locationPhone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        var phone = string.IsNullOrWhiteSpace(locationPhone) ? null : locationPhone.Trim();

        return
            $"Hi {firstName},\n\n" +
            $"You've initiated a service request for your RV with {locationName}. " +
            "Use the link below to provide the service manager with important details about your issue:\n\n" +
            $"{link}\n\n" +
            $"You're receiving this email from RV Intake on behalf of {locationName} because you initiated a service request. " +
            $"This link will work once and expires in {expiryHours} hours.\n\n" +
            $"{QuestionsLine(locationName, phone)}\n";
    }

    private static string QuestionsLine(string location, string? phone) =>
        phone is null
            ? $"If you have questions, please contact {location} directly."
            : $"If you have questions, please contact {location} directly at: {phone}";
}
