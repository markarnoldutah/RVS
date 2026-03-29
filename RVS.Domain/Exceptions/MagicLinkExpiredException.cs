namespace RVS.Domain.Exceptions;

/// <summary>
/// Thrown when a magic-link token has expired.
/// Mapped to HTTP 410 Gone by <c>ExceptionHandlingMiddleware</c>.
/// </summary>
public sealed class MagicLinkExpiredException : Exception
{
    public MagicLinkExpiredException()
        : base("Magic-link token has expired.") { }

    public MagicLinkExpiredException(string message)
        : base(message) { }

    public MagicLinkExpiredException(string message, Exception innerException)
        : base(message, innerException) { }
}
