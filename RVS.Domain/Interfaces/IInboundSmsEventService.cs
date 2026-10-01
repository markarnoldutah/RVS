namespace RVS.Domain.Interfaces;

/// <summary>
/// Handles the two inbound ACS events RVS subscribes to (issue #665): a carrier keyword texted
/// to the sending number, and a delivery report for a text RVS sent.
///
/// This is a keyword handler, not a conversation. Outbound texting stays one-way: no other
/// inbound message is read, stored, routed to anyone or answered
/// (<c>Spec</c>, "Explicitly out of scope").
/// </summary>
public interface IInboundSmsEventService
{
    /// <summary>
    /// Applies an inbound message to the customer records for that number.
    /// <c>STOP</c> and its synonyms set the SMS opt-out and <c>START</c> / <c>UNSTOP</c> clear it,
    /// neither with a reply — the carrier already sent one. <c>HELP</c> is the one keyword RVS
    /// answers itself, with a fixed reply that changes no state and needs no customer record.
    /// Everything else is ignored.
    ///
    /// The sending number is shared across dealers and the carrier blocks it for all of them, so
    /// **every** tenant's profile for the number is updated, not just one. A number that matches
    /// no profile is a no-op: the carrier still enforces its own block.
    /// </summary>
    /// <param name="fromPhoneNumber">The customer's number, as ACS reports it (E.164).</param>
    /// <param name="inboundMessageId">The ACS message id of the inbound text, which is what stops a redelivered event replying twice.</param>
    /// <param name="messageBody">The text exactly as sent.</param>
    /// <param name="receivedAtUtc">When the customer sent it, per the event.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>How many profiles were changed. Zero is normal — a HELP reply changes none.</returns>
    Task<int> HandleInboundMessageAsync(
        string fromPhoneNumber,
        string inboundMessageId,
        string? messageBody,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a delivery report against the invite that was sent with this ACS message id
    /// (<c>Spec A-14</c>). A report for a message RVS does not recognise — an A-2 confirmation,
    /// or an invite whose write failed — is ignored.
    /// </summary>
    /// <param name="acsMessageId">The ACS message id the send returned.</param>
    /// <param name="deliveryStatus">The ACS delivery status, e.g. <c>Delivered</c>.</param>
    /// <param name="receivedAtUtc">When the report was raised.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns><c>true</c> when an invite was updated.</returns>
    Task<bool> HandleDeliveryReportAsync(
        string acsMessageId,
        string? deliveryStatus,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default);
}
