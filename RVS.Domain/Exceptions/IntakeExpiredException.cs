namespace RVS.Domain.Exceptions;

/// <summary>
/// Thrown when a submission reaches a location whose tenant has been disabled for longer than the
/// intake capture window (<c>Spec A-19</c>, issue #478).
/// Mapped to HTTP 410 Gone by <c>ExceptionHandlingMiddleware</c>. The message is shown to the
/// customer, so it says nothing about why the dealer is unavailable.
/// </summary>
public sealed class IntakeExpiredException : Exception
{
    public IntakeExpiredException()
        : base("This location isn't accepting online service requests right now.") { }

    public IntakeExpiredException(string message)
        : base(message) { }

    public IntakeExpiredException(string message, Exception innerException)
        : base(message, innerException) { }
}
