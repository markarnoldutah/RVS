namespace RVS.Domain.Exceptions;

/// <summary>
/// Thrown when a caller has used up a configured allowance, such as the per-advisor,
/// per-location or per-tenant cap on intake invite texts (<c>Spec A-14</c>, issue #663).
/// Mapped to HTTP 429 Too Many Requests by <c>ExceptionHandlingMiddleware</c>.
/// </summary>
public sealed class RateLimitExceededException : Exception
{
    public RateLimitExceededException()
        : base("Too many requests. Try again later.") { }

    public RateLimitExceededException(string message)
        : base(message) { }

    public RateLimitExceededException(string message, Exception innerException)
        : base(message, innerException) { }
}
