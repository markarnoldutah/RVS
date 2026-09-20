namespace RVS.API.Integrations;

/// <summary>
/// Text for the customer service-request confirmation (issue #496): thanks the customer
/// by dealership name, points them at their status page, and includes the dealer phone
/// number when known. Built by <see cref="NotificationOrchestrator"/> and passed to the
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

    public static string BuildEmailHtmlBody(string dealershipName, string statusUrl, string? dealerPhone)
    {
        var phoneParagraph = string.IsNullOrWhiteSpace(dealerPhone)
            ? string.Empty
            : $"<p>Questions? Call {dealershipName} at {dealerPhone}.</p>";

        return
            $"<p>Thank you for submitting your service request to <strong>{dealershipName}</strong>.</p>" +
            $"<p>You can check the status of your request any time on your status page: " +
            $"<a href=\"{statusUrl}\">{statusUrl}</a></p>" +
            phoneParagraph;
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
