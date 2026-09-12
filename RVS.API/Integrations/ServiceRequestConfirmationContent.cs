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
    /// <summary>Single-segment SMS length ceiling (GSM-7, no concatenation).</summary>
    public const int SmsMaxLength = 160;

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
        var withPhone = string.IsNullOrWhiteSpace(dealerPhone)
            ? null
            : $"{dealershipName}: Thanks for your service request. Status: {statusUrl} Questions? Call {dealerPhone}. Reply STOP to opt out.";

        if (withPhone is not null && withPhone.Length <= SmsMaxLength)
        {
            return withPhone;
        }

        // Drop the phone number first — it's the least critical part of the message.
        var withoutPhone = $"{dealershipName}: Thanks for your service request. Status: {statusUrl} Reply STOP to opt out.";
        if (withoutPhone.Length <= SmsMaxLength)
        {
            return withoutPhone;
        }

        // Still too long (e.g. a very long dealership name): fall back to the bare minimum.
        // The status link is the critical piece and is never truncated itself.
        var minimal = $"Service request confirmed. Status: {statusUrl}";
        return minimal;
    }
}
