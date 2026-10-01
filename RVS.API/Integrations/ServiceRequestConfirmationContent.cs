using System.Net;

namespace RVS.API.Integrations;

/// <summary>
/// Text for the customer service-request confirmation (issue #496, email wording #737): thanks
/// the customer by dealership name, points them at their status page, and includes the dealer
/// phone number when known. Built by <see cref="NotificationOrchestrator"/> and passed to the
/// generic email/SMS send methods so both channels say the same thing — the SMS variant
/// truncates sensibly under length pressure but never at the expense of the status link,
/// which is the critical piece of the message.
/// </summary>
internal static class ServiceRequestConfirmationContent
{
    /// <summary>
    /// Two concatenated GSM-7 segments (2 × 153), matching <see cref="IntakeInviteContent"/>.
    /// A confirmation is often the customer's first text from us — the web-form path sends one
    /// to a number nobody here has texted before — so it carries the full compliance tail, and
    /// that tail no longer fits a single segment.
    /// </summary>
    public const int SmsMaxLength = 306;

    /// <summary>
    /// Submitted verbatim as a sample message in the toll-free verification application (#659),
    /// so it changes only in step with that application and with <c>Spec A-2</c>'s disclosure
    /// wording. <c>HELP</c> is answered by the inbound keyword handler (#665).
    /// </summary>
    private const string Compliance = "Msg & data rates may apply. Reply STOP to opt out, HELP for help.";

    public static string BuildEmailSubject(string dealershipName)
        => $"Your Service Request Confirmation — {dealershipName}";

    /// <remarks>
    /// Wording from issue #737, in the same shape as the intake invite (#710): greets the customer,
    /// says the email comes from RV Intake on the dealer's behalf, and sends questions back to the
    /// dealer. Unlike the invite, the status link is not single use, so the email says only when it
    /// expires — and only when that is known, since a reused token keeps its original expiry.
    /// Everything the customer or dealer typed is HTML-encoded.
    /// </remarks>
    public static string BuildEmailHtmlBody(
        string dealershipName, string? customerFirstName, string statusUrl, int? expiresInDays, string? dealerPhone)
    {
        var dealer = WebUtility.HtmlEncode(dealershipName);
        var greeting = string.IsNullOrWhiteSpace(customerFirstName)
            ? "Hi,"
            : $"Hi {WebUtility.HtmlEncode(customerFirstName.Trim())},";
        var href = WebUtility.HtmlEncode(statusUrl);
        var expiry = expiresInDays is { } days
            ? $" This link expires in {days} {(days == 1 ? "day" : "days")}."
            : string.Empty;
        var questions = string.IsNullOrWhiteSpace(dealerPhone)
            ? $"If you have questions, please contact {dealer} directly."
            : $"If you have questions, please contact {dealer} directly at {WebUtility.HtmlEncode(dealerPhone.Trim())}.";

        return
            $"<p>{greeting}</p>" +
            $"<p>Thank you for submitting a service request for your RV to <strong>{dealer}</strong>. " +
            "Use the link below to check your request status at any time:</p>" +
            $"<p><a href=\"{href}\">Check request status</a></p>" +
            $"<p>You're receiving this email from RV Intake on behalf of {dealer} because you submitted a service request.{expiry}</p>" +
            $"<p>{questions}</p>";
    }

    public static string BuildSmsBody(string dealershipName, string statusUrl, string? dealerPhone)
    {
        string[] phoneRung = string.IsNullOrWhiteSpace(dealerPhone)
            ? []
            : [$"{dealershipName}: Thanks for your service request. Status: {statusUrl} Questions? Call {dealerPhone}. {Compliance}"];

        // Sheds detail under length pressure: the phone number first, then the dealership name.
        // The status link is the critical piece and is never truncated itself, and the compliance
        // tail outranks everything but the link — it is what the verification application promises.
        string[] ladder =
        [
            .. phoneRung,
            $"{dealershipName}: Thanks for your service request. Status: {statusUrl} {Compliance}",
            $"Service request confirmed. Status: {statusUrl} {Compliance}",
            $"Service request confirmed. Status: {statusUrl}",
        ];

        return ladder.FirstOrDefault(body => body.Length <= SmsMaxLength) ?? ladder[^1];
    }
}
