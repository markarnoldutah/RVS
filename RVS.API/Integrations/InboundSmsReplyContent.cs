namespace RVS.API.Integrations;

/// <summary>
/// The reply to an inbound <c>HELP</c> (issue #665). Every message RVS sends promises
/// <i>Reply … HELP for help</i>, and this is the answer.
///
/// <b>RVS does not send it.</b> Twilio's Advanced Opt-Out answers HELP itself, so this string is
/// the source of truth that is pasted into each environment's Messaging Service as its HELP
/// reply. Change it here, in the test that pins it, and in both Messaging Services together.
///
/// One fixed string, no lookup. An inbound text carries a phone number and nothing else, so the
/// reply cannot name the dealership, and points at it instead. It names RV Intake, says who RVS sends on behalf of (the third-party disclosure the
/// CTIA guidelines ask for), carries the rates line and repeats STOP.
///
/// **This string is submitted verbatim as a sample message on the toll-free verification
/// application (#659), so the test that pins it is protecting an external commitment, not style.**
/// </summary>
internal static class InboundSmsReplyContent
{
    /// <summary>
    /// 194 characters: two concatenated GSM-7 segments. A one-segment version would have to drop
    /// either the third-party naming or the rates line, and both were kept deliberately — HELP is
    /// rare, and this is the message a carrier reviewer reads.
    /// </summary>
    public const string Help =
        "RV Intake: We send service-request links and confirmations for your RV dealership. " +
        "For help with your request, contact the dealership directly. " +
        "Msg & data rates may apply. Reply STOP to opt out.";
}
