using Azure.Storage.Blobs;
using RVS.API.HealthChecks;
using RVS.API.Middleware;
using RVS.API.Services;
using RVS.Domain.Interfaces;
using RVS.Infra.AzCosmosRepository.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

// Configure CORS — AllowBlazorClient for Cust_Intake WASM + Mngr_Desktop WASM
if (builder.Environment.IsProduction())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorClient", corsBuilder =>
        {
            corsBuilder
                .WithOrigins("https://portal.rvserviceflow.com")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}
else
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorClient", corsBuilder =>
        {
            corsBuilder
                .WithOrigins(
                    "https://localhost:7008",
                    "http://localhost:7008",
                    "https://localhost:5001",
                    "http://localhost:5001",
                    "https://localhost:7116",
                    "http://localhost:5236"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

// Auth0 JWT Bearer authentication — audience: https://api.rvserviceflow.com
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Auth0:Domain"];
    options.Audience = builder.Configuration["Auth0:Audience"];
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = actionContext =>
        {
            var actionExecutingContext = actionContext as Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext;

            if (actionContext.ModelState.ErrorCount > 0
                && actionExecutingContext?.ActionArguments.Count == actionContext.ActionDescriptor.Parameters.Count)
            {
                return new UnprocessableEntityObjectResult(actionContext.ModelState);
            }

            return new BadRequestObjectResult(actionContext.ModelState);
        };
    });

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Register Middleware
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();

// Cosmos DB client
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var endpoint = builder.Configuration["CosmosDb:Endpoint"]
        ?? throw new InvalidOperationException("CosmosDb:Endpoint configuration is missing.");
    var key = builder.Configuration["CosmosDb:Key"]
        ?? throw new InvalidOperationException("CosmosDb:Key configuration is missing.");

    return new CosmosClient(endpoint, key);
});

// Blob Storage client
builder.Services.AddSingleton<BlobServiceClient>(sp =>
{
    var connectionString = builder.Configuration["BlobStorage:ConnectionString"]
        ?? throw new InvalidOperationException("BlobStorage:ConnectionString configuration is missing.");

    return new BlobServiceClient(connectionString);
});

#region Repositories
builder.Services.AddScoped<IConfigRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "rvsdb";
    return new CosmosConfigRepository(client, databaseId, "tenants");
});

builder.Services.AddScoped<ILookupRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "rvsdb";
    return new CosmosLookupRepository(client, databaseId, "lookups");
});

// TODO: Register repository implementations when Infra layer is complete
// builder.Services.AddScoped<IServiceRequestRepository, CosmosServiceRequestRepository>();
// builder.Services.AddScoped<ICustomerProfileRepository, CosmosCustomerProfileRepository>();
// builder.Services.AddScoped<IGlobalCustomerAcctRepository, CosmosGlobalCustomerAcctRepository>();
// builder.Services.AddScoped<IDealershipRepository, CosmosDealershipRepository>();
// builder.Services.AddScoped<ILocationRepository, CosmosLocationRepository>();
// builder.Services.AddScoped<IAssetLedgerRepository, CosmosAssetLedgerRepository>();
// builder.Services.AddScoped<ISlugLookupRepository, CosmosSlugLookupRepository>();
// builder.Services.AddScoped<ITenantAccessRepository, TablesTenantAccessRepository>();
// builder.Services.AddScoped<ITenantConfigRepository, CosmosTenantConfigRepository>();
#endregion

#region Services
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<ILookupService, LookupService>();

// TODO: Register service implementations when Service layer is complete
// builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
// builder.Services.AddScoped<ICustomerProfileService, CustomerProfileService>();
// builder.Services.AddScoped<IGlobalCustomerAcctService, GlobalCustomerAcctService>();
// builder.Services.AddScoped<IDealershipService, DealershipService>();
// builder.Services.AddScoped<ILocationService, LocationService>();
// builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();
#endregion

// Claims Management
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextAccessor, HttpUserContextAccessor>();
builder.Services.AddScoped<ClaimsService>();

// Health Checks — Cosmos DB + Blob Storage probes
builder.Services.AddHealthChecks()
    .AddCheck<CosmosDbHealthCheck>("cosmos-db", tags: ["ready"])
    .AddCheck<BlobStorageHealthCheck>("blob-storage", tags: ["ready"]);

var app = builder.Build();

// 1. Dev-only endpoints (OpenAPI, Swagger UI)
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
        options.DocumentTitle = "RVS API";
        options.OAuthClientId(builder.Configuration["Auth0:ClientId"]);
        options.OAuthClientSecret(builder.Configuration["Auth0:ClientSecret"]);
        options.OAuthAppName("RVS API");
        options.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
         {
             { "audience", builder.Configuration["Auth0:Audience"] ?? "" }
         });
        options.OAuthScopes("openid", "profile");
        options.OAuthUsePkce();
        options.EnablePersistAuthorization();
    });
}

// 2. HTTPS redirection (production only)
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// 3. CORS
app.UseCors("AllowBlazorClient");

// 4. Structured logging — enriches log scope with tenantId, locationId, correlationId
app.UseMiddleware<CorrelationLoggingMiddleware>();

// 5. ExceptionHandlingMiddleware (IMiddleware, singleton)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 7. Tenant access gate
app.UseMiddleware<TenantAccessGateMiddleware>();

// 8. Health endpoint (no auth required)
app.MapHealthChecks("/health");

// 9. Map controllers
app.MapControllers();

app.Run();

/// <summary>
/// Document transformer to add OAuth2/JWT Bearer authentication to OpenAPI for SwaggerUI
/// </summary>
internal sealed class BearerSecuritySchemeTransformer(IAuthenticationSchemeProvider authenticationSchemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        var authenticationSchemes = await authenticationSchemeProvider.GetAllSchemesAsync();
        if (authenticationSchemes.Any(authScheme => authScheme.Name == "Bearer"))
        {
            // Add the security scheme at the document level
            var securitySchemes = new Dictionary<string, IOpenApiSecurityScheme>
            {
                ["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.OAuth2,
                    Flows = new OpenApiOAuthFlows
                    {
                        Implicit = new OpenApiOAuthFlow
                        {
                            AuthorizationUrl = new Uri($"{context.ApplicationServices.GetRequiredService<IConfiguration>()["Auth0:Domain"]}/authorize"),
                            TokenUrl = new Uri($"{context.ApplicationServices.GetRequiredService<IConfiguration>()["Auth0:Domain"]}oauth/token"),
                            Scopes = new Dictionary<string, string>
                            {
                                { "openid", "OpenID" },
                                { "profile", "Profile" }
                            }
                        }
                    },
                    In = ParameterLocation.Header,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token."
                }
            };
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes = securitySchemes;

            // Apply it as a requirement for all operations
            foreach (var operation in document.Paths.Values.SelectMany(path => path.Operations))
            {
                operation.Value.Security ??= [];
                operation.Value.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
            }
        }
    }
}