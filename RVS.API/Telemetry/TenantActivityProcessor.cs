using System.Diagnostics;
using OpenTelemetry;

namespace RVS.API.Telemetry;

/// <summary>
/// OpenTelemetry activity processor that stamps every trace/request with
/// <c>TenantId</c>, <c>LocationIds</c>, and <c>CorrelationId</c> custom dimensions
/// so that Application Insights queries can be filtered and grouped by tenant.
/// Reads claims from the current <see cref="HttpContext"/> (populated after authentication middleware).
/// </summary>
public sealed class TenantActivityProcessor : BaseProcessor<Activity>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal const string TenantIdClaimType = "https://rvserviceflow.com/tenantId";
    internal const string LocationIdsClaimType = "https://rvserviceflow.com/locationIds";
    internal const string CorrelationIdHeader = "X-Correlation-ID";

    public TenantActivityProcessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public override void OnEnd(Activity data)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is not null)
        {
            var tenantId = httpContext.User?.FindFirst(TenantIdClaimType)?.Value;
            if (!string.IsNullOrEmpty(tenantId))
            {
                data.SetTag("TenantId", tenantId);
            }

            var locationIds = httpContext.User?.FindFirst(LocationIdsClaimType)?.Value;
            if (!string.IsNullOrEmpty(locationIds))
            {
                data.SetTag("LocationIds", locationIds);
            }

            var correlationId = httpContext.Response.Headers[CorrelationIdHeader].FirstOrDefault()
                ?? httpContext.Request.Headers[CorrelationIdHeader].FirstOrDefault();
            if (!string.IsNullOrEmpty(correlationId))
            {
                data.SetTag("CorrelationId", correlationId);
            }
        }

        base.OnEnd(data);
    }
}
