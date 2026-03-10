using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace RVS.API.Middleware
{
    public class ExceptionHandlingMiddleware : IMiddleware
    {
        private readonly ILogger<ExceptionHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _env;
        const string tenantIdClaim = "http://benefetch.com/tenantId";

        public ExceptionHandlingMiddleware(
            ILogger<ExceptionHandlingMiddleware> logger,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _env = env;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            try
            {
                await next(context);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            var errorId = Guid.NewGuid();
            var tenantId = context.User?.FindFirst(tenantIdClaim)?.Value;
            var userId = context.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

            var statusCode = HttpStatusCode.InternalServerError;
            var clientMessage = "A server error occurred. Please contact support with this ID.";

            // Map known exception types → status codes
            switch (exception)
            {
                case ArgumentException:
                    statusCode = HttpStatusCode.BadRequest;
                    clientMessage = "The request was invalid.";
                    break;

                // If you add a ValidationException type:
                // case ValidationException:
                //     statusCode = HttpStatusCode.BadRequest;
                //     clientMessage = "The request was invalid.";
                //     break;

                case UnauthorizedAccessException:
                    statusCode = HttpStatusCode.Unauthorized;
                    clientMessage = "Not authorized.";
                    break;

                case KeyNotFoundException:
                    statusCode = HttpStatusCode.NotFound;
                    clientMessage = "Resource not found.";
                    break;

                // Optional domain conflict:
                // case ConflictException:
                //     statusCode = HttpStatusCode.Conflict;
                //     clientMessage = "The request could not be completed due to a conflict.";
                //     break;

                default:
                    statusCode = HttpStatusCode.InternalServerError;
                    clientMessage = "A server error occurred. Please contact support with this ID.";
                    break;
            }


            // Log with full detail (for dev/support), but do NOT send to client
            _logger.LogError(exception,
                "Unhandled exception. errorId={ErrorId}, statusCode={StatusCode}, tenantId={TenantId}, userId={UserId}, path={Path}",
                errorId,
                (int)statusCode,
                tenantId,
                userId,
                context.Request.Path);

            // Build HIPAA-safe response
            object errorPayload = _env.IsDevelopment()
                ? new
                {
                    message = clientMessage,
                    errorId,
                    // extra fields for dev only (no PHI)
                    exception = exception.Message,
                    stackTrace = exception.StackTrace
                }
                : new
                {
                    message = clientMessage,
                    errorId
                };

            context.Response.Clear();
            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json";

            var json = JsonSerializer.Serialize(errorPayload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await context.Response.WriteAsync(json);
        }
    }
}
