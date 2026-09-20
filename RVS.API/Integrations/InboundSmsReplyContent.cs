namespace RVS.API.Integrations;

/// <summary>
/// The one reply RVS sends to an inbound text (issue #665). Only <c>HELP</c> gets one: carriers
/// answer <c>STOP</c>, <c>START</c> and <c>UNSTOP</c> themselves on a toll-free number, but not
/// this, and every message RVS sends promises <i>Reply … HELP for help</i>.
///
/// One hardcoded string, no lookup. The handler has no tenant and no location — an inbound text
/// carries a phone number and nothing else — so it cannot name the dealership, and points at it
/// instead. It names RV Intake, says who RVS sends on behalf of (the third-party disclosure the
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
