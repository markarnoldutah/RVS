namespace RVS.Domain.Interfaces;

/// <summary>
/// Handles the two inbound Twilio messaging webhooks RVS subscribes to (issue #665): a carrier
/// keyword texted to the sending number, and a status callback for a text RVS sent.
///
/// This is a keyword handler, not a conversation. Outbound texting stays one-way: no other
/// inbound message is read, stored, routed to anyone or answered
/// (<c>Spec</c>, "Explicitly out of scope").
/// </summary>
public interface IInboundSmsEventService
{
    /// <summary>
    /// Applies an inbound message to the customer records for that number.
    /// <c>STOP</c> and its synonyms set the SMS opt-out and <c>START</c> / <c>UNSTOP</c> clear it.
    /// RVS sends no reply to any keyword: Twilio's Advanced Opt-Out answers STOP, START and HELP
    /// itself, and HELP changes no state. Everything else is ignored.
    ///
    /// The sending number is shared across dealers and the carrier blocks it for all of them, so
    /// **every** tenant's profile for the number is updated, not just one. A number that matches
    /// no profile is a no-op: the carrier still enforces its own block.
    /// </summary>
    /// <param name="fromPhoneNumber">The customer's number, as Twilio reports it (E.164).</param>
    /// <param name="inboundMessageId">The Twilio Message SID of the inbound text.</param>
    /// <param name="messageBody">The text exactly as sent.</param>
    /// <param name="receivedAtUtc">When RVS received it. Twilio's inbound webhook carries no timestamp.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many profiles were changed. Zero is normal — HELP changes none.</returns>
    Task<int> HandleInboundMessageAsync(
        string fromPhoneNumber,
        string inboundMessageId,
        string? messageBody,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a status callback against the invite that was sent with this Twilio Message SID
    /// (<c>Spec A-14</c>). A callback for a message RVS does not recognise — an A-2 confirmation,
    /// or an invite whose write failed — is ignored, as is any status that is not terminal.
    /// </summary>
    /// <param name="providerMessageId">The Twilio Message SID the send returned.</param>
    /// <param name="deliveryStatus">Twilio's <c>MessageStatus</c>, e.g. <c>delivered</c> or <c>undelivered</c>.</param>
    /// <param name="receivedAtUtc">When RVS received the callback.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when an invite was updated.</returns>
    Task<bool> HandleDeliveryReportAsync(
        string providerMessageId,
        string? deliveryStatus,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);
}
