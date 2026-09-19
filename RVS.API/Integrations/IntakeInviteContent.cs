namespace RVS.API.Integrations;

/// <summary>
/// Text of an advisor intake invite (<c>Spec A-14</c>, issue #663): who it is from, a greeting,
/// the link, and the opt-out line. Laddered like <see cref="ServiceRequestConfirmationContent"/>:
/// under length pressure it drops the greeting, then the location name, then the opt-out line,
/// and never shortens the link, which is the whole message.
/// </summary>
internal static class IntakeInviteContent
{
    /// <summary>
    /// Two concatenated GSM-7 segments (2 × 153). The invite link alone is about 100 characters,
    /// so one segment would lose the sender and the opt-out line on almost every send, and a
    /// first text to a new number needs both.
    /// </summary>
    public const int SmsMaxLength = 306;

    private const string OptOut = "Reply STOP to opt out.";

    public static string BuildSmsBody(string locationName, string firstName, string link)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(firstName);
        ArgumentException.ThrowIfNullOrWhiteSpace(link);

        string[] ladder =
        [
            $"{locationName}: Hi {firstName}, here's the link to start your service request: {link} {OptOut}",
            $"{locationName}: Here's the link to start your service request: {link} {OptOut}",
            $"{link} {OptOut}",
        ];

        return ladder.FirstOrDefault(body => body.Length <= SmsMaxLength) ?? link;
    }
}
