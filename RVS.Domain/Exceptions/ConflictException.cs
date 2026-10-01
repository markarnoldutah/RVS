namespace RVS.Domain.Exceptions;

/// <summary>
/// Thrown when a create-only write collides with a resource that already exists — a taken
/// slug, a tenant id already in use, or an identity-provider user owned by another tenant.
/// Mapped to HTTP 409 Conflict by <c>ExceptionHandlingMiddleware</c>.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException()
        : base("The resource already exists.") { }

    public ConflictException(string message)
        : base(message) { }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException) { }
}
