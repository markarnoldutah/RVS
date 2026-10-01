namespace RVS.Domain.Integrations;

/// <summary>
/// Remembers inbound messages RVS has already replied to (issue #665). Event Grid delivers each
/// event at least once, and a redelivered <c>HELP</c> would otherwise text the customer twice.
///
/// Only the reply needs this. <c>STOP</c> and <c>START</c> are already idempotent: they set a
/// flag, and an event at or before the stored keyword time is ignored.
/// </summary>
public interface IInboundSmsDeduplicator
{
    /// <summary>
    /// Records that this inbound message is being handled, and reports whether it is the first
    /// time. Called once per reply-worthy event.
    /// </summary>
    /// <param name="inboundMessageId">The ACS message id of the inbound text.</param>
    /// <returns><c>true</c> when this message has not been seen before and a reply should go out.</returns>
    bool TryBeginHandling(string inboundMessageId);
}
