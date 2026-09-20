namespace RVS.API.Integrations;

/// <summary>
/// Text of an advisor intake invite (<c>Spec A-14</c>, issue #663): who it is from, a greeting,
/// the link, and the compliance tail. Laddered like <see cref="ServiceRequestConfirmationContent"/>:
/// under length pressure it drops the greeting, then the location name, then the compliance tail,
/// and never shortens the link, which is the whole message.
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
}
