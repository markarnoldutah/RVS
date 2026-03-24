namespace RVS.API.Middleware;

/// <summary>
/// Enriches the log scope with tenantId, locationId, and correlation ID for every request.
/// Uses <c>X-Correlation-ID</c> header if present, otherwise generates a new GUID.
/// </summary>
public sealed class CorrelationLoggingMiddleware
{
    private readonly RequestDelegate _next;

    private const string CorrelationIdHeader = "X-Correlation-ID";
    private const string TenantIdClaimType = "https://rvserviceflow.com/tenantId";
    private const string LocationIdClaimType = "https://rvserviceflow.com/locationId";

    public CorrelationLoggingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationLoggingMiddleware> logger)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Response.Headers[CorrelationIdHeader] = correlationId;

        var tenantId = context.User?.FindFirst(TenantIdClaimType)?.Value ?? "anonymous";
        var locationId = context.User?.FindFirst(LocationIdClaimType)?.Value ?? "unknown";

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TenantId"] = tenantId,
            ["LocationId"] = locationId
        }))
        {
            await _next(context);
        }
    }
}
