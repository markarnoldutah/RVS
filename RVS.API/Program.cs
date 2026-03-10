using RVS.API.Middleware;
using RVS.API.Services;
using RVS.Domain.Interfaces;
using RVS.Infra.AzCosmosRepository.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Azure.Cosmos;
using Microsoft.OpenApi;
using RVS.API.Integrations.Availity;
using RVS.API.Integrations.Availity.Mock;
using Microsoft.Extensions.Http.Resilience;
using Polly;
using System;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Configure CORS - Production uses restricted policy, Development uses open policy
if (builder.Environment.IsProduction())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("ProductionCors", corsBuilder =>
        {
            corsBuilder
                .WithOrigins("https://portal.benefetch.com")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}
else
{
    // Development: Allow localhost for testing
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("DevelopmentCors", corsBuilder =>
        {
            corsBuilder
                .WithOrigins(
                    "https://localhost:7008",  // Blazor WASM app
                    "http://localhost:7008",   // Blazor WASM app (HTTP)
                    "https://localhost:5001",  // Alternative Blazor WASM port
                    "http://localhost:5001",   // Alternative Blazor WASM port (HTTP)
                    "https://localhost:7116",  // API (for Swagger)
                    "http://localhost:5236"    // API (HTTP)
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}

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
        // MODEL VALIDATION
        // TODO confirm we need to correct model validation in API
        options.InvalidModelStateResponseFactory = actionContext =>
        {
            var actionExecutingContext = actionContext as Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext;

            // if there are modelstate errors & all keys were correctly found/parsed we're dealing with validation errors.  
            // By default a 400 BadRequest is returned; we modify below to return a more appropriate 422
            if (actionContext.ModelState.ErrorCount > 0
                && actionExecutingContext?.ActionArguments.Count == actionContext.ActionDescriptor.Parameters.Count)
            {
                return new UnprocessableEntityObjectResult(actionContext.ModelState);
            }

            // if one of the keys wasn't correctly found / couldn't be parsed
            // we're dealing with null/unparsable input so return 400 BadRequest
            return new BadRequestObjectResult(actionContext.ModelState);
        };

    });

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    // Add document transformer to configure OAuth2/JWT Bearer security
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

// Register Middleware
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();

// Add CosmosClient
builder.Services.AddSingleton<CosmosClient>(sp =>
{
    var endpoint = builder.Configuration["CosmosDb:Endpoint"] 
        ?? throw new InvalidOperationException("CosmosDb:Endpoint configuration is missing.");
    var key = builder.Configuration["CosmosDb:Key"] 
        ?? throw new InvalidOperationException("CosmosDb:Key configuration is missing.");
    
    return new CosmosClient(endpoint, key);
});

#region Add repositories
builder.Services.AddScoped<IConfigRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "bfdb";
    return new CosmosConfigRepository(client, databaseId, "tenants", "payers");
});

builder.Services.AddScoped<ILookupRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "bfdb";
    return new CosmosLookupRepository(client, databaseId, "lookups");
});

// Patient repository now includes embedded encounters (partition key: practiceId)
builder.Services.AddScoped<IPatientRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "bfdb";
    return new CosmosPatientRepository(client, databaseId, "patients");
});

builder.Services.AddScoped<IPayerRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "bfdb";
    return new CosmosPayerRepository(client, databaseId, "payers");
});

builder.Services.AddScoped<IPracticeRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var databaseId = builder.Configuration["CosmosDb:DatabaseId"] ?? "bfdb";
    return new CosmosPracticeRepository(client, databaseId, "practices");
});
#endregion

// Add services
// Focused services for patient aggregate operations (SRP refactoring)
builder.Services.AddScoped<ITenantService, TenantService>();
builder.Services.AddScoped<IPayerService, PayerService>();
builder.Services.AddScoped<IPracticeService, PracticeService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IPatientService, PatientService>();
builder.Services.AddScoped<ICoverageEnrollmentService, CoverageEnrollmentService>();
builder.Services.AddScoped<IEncounterService, EncounterService>();

// Eligibility check service (consolidated - includes CRUD + clearinghouse integration)
builder.Services.AddScoped<IEligibilityCheckService, EligibilityCheckService>();

// Single-screen check-in workflow service (RU-optimized)
builder.Services.AddScoped<IPatientCheckInService, PatientCheckInService>();

// Claims Management
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextAccessor, HttpUserContextAccessor>();
builder.Services.AddScoped<ClaimsService>();

// =====================================================
// Eligibility Check Engine (Availity)
// =====================================================
builder.Services.AddOptions<AvailityOptions>()
    .Bind(builder.Configuration.GetSection("Availity"));

// Check if we should use mock
var useMock = builder.Configuration.GetValue<bool>("AvailityMock:UseMock");

if (useMock)
{
    // Register mock client for development/testing
    var mockOptions = builder.Configuration.GetSection("AvailityMock").Get<MockAvailityOptions>() 
        ?? new MockAvailityOptions();
    
    // ? CHANGED: Singleton instead of Scoped to preserve poll counts across requests
    builder.Services.AddSingleton<IAvailityEligibilityClient>(sp =>
    {
        var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<MockAvailityEligibilityClient>>();
        return new MockAvailityEligibilityClient(mockOptions, logger)
        {
            Scenario = mockOptions.DefaultScenario,
            SimulatedDelayMs = mockOptions.SimulatedDelayMs
        };
    });
    
    // Also register the concrete type for middleware access
    builder.Services.AddSingleton<MockAvailityEligibilityClient>(sp =>
        (MockAvailityEligibilityClient)sp.GetRequiredService<IAvailityEligibilityClient>());
}
else
{
    // Register real Availity client
    builder.Services.AddTransient<AvailityAuthHandler>();

    builder.Services.AddHttpClient<IAvailityEligibilityClient, AvailityEligibilityClient>((sp, client) =>
        {
            var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AvailityOptions>>().Value;

            client.BaseAddress = new Uri(opt.BaseUrl);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddHttpMessageHandler<AvailityAuthHandler>()
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.BackoffType = DelayBackoffType.Exponential;
            options.Retry.UseJitter = true;
            options.Retry.Delay = TimeSpan.FromSeconds(1);
            options.Retry.MaxDelay = TimeSpan.FromSeconds(5);

            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
            
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
        });
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
        options.DocumentTitle = "Beneftech API";
        // OAuth configuration for Auth0
        options.OAuthClientId(builder.Configuration["Auth0:ClientId"]);
        options.OAuthClientSecret(builder.Configuration["Auth0:ClientSecret"]);
        options.OAuthAppName("Beneftech API");
        options.OAuthAdditionalQueryStringParams(new Dictionary<string, string>
         {
             { "audience", builder.Configuration["Auth0:Audience"] ?? "" }
         });
        options.OAuthScopes("openid", "profile");
        options.OAuthUsePkce();
        options.EnablePersistAuthorization();
    });
    
    // Enable mock scenario selection via HTTP header (Development only)
    if (useMock)
    {
        app.UseMockScenarioMiddleware();
    }
}

// Production: Enforce HTTPS redirection
if (app.Environment.IsProduction())
{
    app.UseHttpsRedirection();
}

// Use environment-specific CORS policy
app.UseCors(app.Environment.IsProduction() ? "ProductionCors" : "DevelopmentCors");

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

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