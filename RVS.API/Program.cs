using Azure.Data.Tables;
using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using RVS.API.HealthChecks;
using RVS.API.Integrations;
using RVS.API.Middleware;
using RVS.Infra.AzBlobRepository;
using RVS.API.Services;
using RVS.Domain.Integrations;
using RVS.Domain.Interfaces;
using RVS.Infra.AzCosmosRepository.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.OpenApi;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Azure Key Vault configuration provider — loads secrets from Key Vault in staging/production.
// The KeyVault:VaultUri app setting is injected by Bicep (app-service-config.bicep → KeyVault__VaultUri).
// In Development, this is skipped — secrets come from appsettings.Development.json or dotnet user-secrets.
var keyVaultUri = builder.Configuration["KeyVault:VaultUri"];
if (!string.IsNullOrEmpty(keyVaultUri))
{
    builder.Configuration.AddAzureKeyVault(
        new Uri(keyVaultUri),
        new DefaultAzureCredential());
}

// Configure CORS — AllowBlazorClient for Blazor.Intake WASM + Blazor.Manager WASM
if (builder.Environment.IsProduction())
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorClient", corsBuilder =>
        {
            corsBuilder
                .WithOrigins("https://intake.rvserviceflow.com", "https://manager.rvserviceflow.com")
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        });
    });
}
else if (builder.Environment.IsEnvironment("Staging"))
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorClient", corsBuilder =>
        {
            corsBuilder
                .WithOrigins(
                    "https://zealous-island-0ff7ab71e.6.azurestaticapps.net", // intake
                    "https://mango-grass-08484a41e.1.azurestaticapps.net") // manager
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
                    "http://localhost:5236",
                    "https://localhost:7200",
                    "http://localhost:5200",
                    "https://localhost:7300",
                    "http://localhost:5300"
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

builder.Services.AddAuthorization(options =>
{
    // Service Requests
    options.AddPolicy("CanReadServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:read"));
    options.AddPolicy("CanSearchServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:search"));
    options.AddPolicy("CanUpdateServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:update"));
    options.AddPolicy("CanUpdateServiceEvent", policy =>
        policy.RequireClaim("permissions", "service-requests:update-service-event"));
    options.AddPolicy("CanDeleteServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:delete"));

    // Attachments
    options.AddPolicy("CanUploadAttachments", policy =>
        policy.RequireClaim("permissions", "attachments:upload"));
    options.AddPolicy("CanReadAttachments", policy =>
        policy.RequireClaim("permissions", "attachments:read"));
    options.AddPolicy("CanDeleteAttachments", policy =>
        policy.RequireClaim("permissions", "attachments:delete"));

    // Dealerships
    options.AddPolicy("CanReadDealerships", policy =>
        policy.RequireClaim("permissions", "dealerships:read"));
    options.AddPolicy("CanUpdateDealerships", policy =>
        policy.RequireClaim("permissions", "dealerships:update"));

    // Locations
    options.AddPolicy("CanReadLocations", policy =>
        policy.RequireClaim("permissions", "locations:read"));
    options.AddPolicy("CanCreateLocations", policy =>
        policy.RequireClaim("permissions", "locations:create"));
    options.AddPolicy("CanUpdateLocations", policy =>
        policy.RequireClaim("permissions", "locations:update"));

    // Analytics
    options.AddPolicy("CanReadAnalytics", policy =>
        policy.RequireClaim("permissions", "analytics:read"));

    // Tenant Config — accepts any of tenants:config:read/create/update
    options.AddPolicy("CanManageTenantConfig", policy =>
        policy.RequireClaim("permissions", "tenants:config:read", "tenants:config:create", "tenants:config:update"));

    // Lookups
    options.AddPolicy("CanReadLookups", policy =>
        policy.RequireClaim("permissions", "lookups:read"));

    // Platform Admin
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireClaim("permissions", "platform:tenants:manage"));
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

// Rate limiting — protects public intake + status endpoints
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("StatusEndpoint", cfg =>
    {
        cfg.PermitLimit = 10;
        cfg.Window = TimeSpan.FromMinutes(1);
    });
    options.AddFixedWindowLimiter("IntakeEndpoint", cfg =>
    {
        cfg.PermitLimit = 20;
        cfg.Window = TimeSpan.FromMinutes(1);
    });
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

// Blob Storage client — BlobStorage:Endpoint + DefaultAzureCredential (Managed Identity / user delegation SAS)
// In development, use AzureCliCredential directly to avoid the ~15 s timeout while
// DefaultAzureCredential probes ManagedIdentityCredential before falling through.
builder.Services.AddSingleton<BlobServiceClient>(sp =>
{
    var endpoint = builder.Configuration["BlobStorage:Endpoint"]
        ?? throw new InvalidOperationException("BlobStorage:Endpoint configuration is missing.");

    TokenCredential credential = builder.Environment.IsDevelopment()
        ? new AzureCliCredential()
        : new DefaultAzureCredential();

    return new BlobServiceClient(new Uri(endpoint), credential);
});

// Azure Tables client
builder.Services.AddSingleton<TableServiceClient>(sp =>
{
    var connectionString = builder.Configuration["AzureTables:ConnectionString"]
        ?? throw new InvalidOperationException("AzureTables:ConnectionString configuration is missing.");

    return new TableServiceClient(connectionString);
});

#region Repositories
var cosmosDbId = builder.Configuration["CosmosDb:DatabaseId"] ?? "rvs-db";

builder.Services.AddScoped<ILookupRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosLookupRepository>>();
    return new CosmosLookupRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<IServiceRequestRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosServiceRequestRepository>>();
    return new CosmosServiceRequestRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<ICustomerProfileRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosCustomerProfileRepository>>();
    return new CosmosCustomerProfileRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<IGlobalCustomerAcctRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosGlobalCustomerAcctRepository>>();
    return new CosmosGlobalCustomerAcctRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<IDealershipRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosDealershipRepository>>();
    return new CosmosDealershipRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<ILocationRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosLocationRepository>>();
    return new CosmosLocationRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<IAssetLedgerRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosAssetLedgerRepository>>();
    return new CosmosAssetLedgerRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<ISlugLookupRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosSlugLookupRepository>>();
    return new CosmosSlugLookupRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<ITenantConfigRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosTenantConfigRepository>>();
    return new CosmosTenantConfigRepository(client, cosmosDbId, logger);
});

// ITenantAccessRepository — implemented in RVS.Infra.AzTablesRepository (registered separately when ready)
// builder.Services.AddScoped<ITenantAccessRepository, TablesTenantAccessRepository>();
#endregion

#region Services
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IDealershipService, DealershipService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();
builder.Services.AddScoped<ICustomerProfileService, CustomerProfileService>();
builder.Services.AddScoped<IGlobalCustomerAcctService, GlobalCustomerAcctService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IIntakeOrchestrationService, IntakeOrchestrationService>();
#endregion

#region Integration Clients
var useMockIntegrations = builder.Configuration.GetValue<bool>("Integrations:UseMocks");

// AI options — payload limits and allowed media types for all AI endpoints
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

// VIN Decoder
if (useMockIntegrations)
{
    builder.Services.AddSingleton<IVinDecoderService, MockVinDecoderService>();
}
else
{
    builder.Services.AddHttpClient<IVinDecoderService, NhtsaVinDecoderClient>(client =>
    {
        client.BaseAddress = new Uri("https://vpic.nhtsa.dot.gov/api/");
    })
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(6);
    });
}

// VIN Extraction (AI Vision)
if (useMockIntegrations)
{
    builder.Services.AddSingleton<IVinExtractionService, MockVinExtractionService>();
}
else
{
    var openAiEndpoint = builder.Configuration["AzureOpenAi:Endpoint"];
    if (!string.IsNullOrWhiteSpace(openAiEndpoint))
    {
        var visionDeploymentName = builder.Configuration["AzureOpenAi:VisionDeploymentName"]
            ?? builder.Configuration["AzureOpenAi:DeploymentName"]
            ?? "gpt-4o";
        builder.Services.AddHttpClient<IVinExtractionService, AzureOpenAiVinExtractionService>(client =>
        {
            var baseUrl = openAiEndpoint.TrimEnd('/') + $"/openai/deployments/{visionDeploymentName}/";
            client.BaseAddress = new Uri(baseUrl);
            var apiKey = builder.Configuration["AzureOpenAi:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);
            }
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        });
    }
    else
    {
        // AzureOpenAi:Endpoint is required when Integrations:UseMocks is false.
        throw new InvalidOperationException(
            "AzureOpenAi:Endpoint must be configured when Integrations:UseMocks is false. " +
            "Either set the endpoint or enable mocks for local development.");
    }
}

// Speech-to-Text (Azure OpenAI Whisper) — uses a dedicated endpoint in northcentralus
// because Whisper 001 Standard is not available in westus3.
if (useMockIntegrations)
{
    builder.Services.AddSingleton<ISpeechToTextService, MockSpeechToTextService>();
}
else
{
    var whisperEndpoint = builder.Configuration["AzureOpenAi:WhisperEndpoint"]
        ?? builder.Configuration["AzureOpenAi:Endpoint"];
    if (!string.IsNullOrWhiteSpace(whisperEndpoint))
    {
        var whisperDeploymentName = builder.Configuration["AzureOpenAi:WhisperDeploymentName"] ?? "whisper";
        builder.Services.AddHttpClient<ISpeechToTextService, AzureWhisperSpeechToTextService>(client =>
        {
            var baseUrl = whisperEndpoint.TrimEnd('/') + $"/openai/deployments/{whisperDeploymentName}/";
            client.BaseAddress = new Uri(baseUrl);
            var apiKey = builder.Configuration["AzureOpenAi:WhisperApiKey"]
                ?? builder.Configuration["AzureOpenAi:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);
            }
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(20);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(40);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(40); // must be >= 2 × AttemptTimeout
        });
    }
    else
    {
        // AzureOpenAi:WhisperEndpoint (or AzureOpenAi:Endpoint fallback) is required when mocks are disabled.
        // Fall back to mock so startup is not blocked during initial onboarding.
        builder.Services.AddSingleton<ISpeechToTextService, MockSpeechToTextService>();
    }
}

// Issue Text Refinement + Category Suggestion
if (useMockIntegrations)
{
    builder.Services.AddSingleton<IIssueTextRefinementService, RuleBasedIssueTextRefinementService>();
}
else
{
    var openAiEndpoint = builder.Configuration["AzureOpenAi:Endpoint"];
    if (!string.IsNullOrWhiteSpace(openAiEndpoint))
    {
        var textDeploymentName = builder.Configuration["AzureOpenAi:TextDeploymentName"]
            ?? builder.Configuration["AzureOpenAi:DeploymentName"]
            ?? "gpt-4o";
        builder.Services.AddHttpClient<IIssueTextRefinementService, AzureOpenAiIssueTextRefinementService>(client =>
        {
            var baseUrl = openAiEndpoint.TrimEnd('/') + $"/openai/deployments/{textDeploymentName}/";
            client.BaseAddress = new Uri(baseUrl);
            var apiKey = builder.Configuration["AzureOpenAi:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                client.DefaultRequestHeaders.Add("api-key", apiKey);
            }
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
        });
    }
    else
    {
        // Fall back to rule-based when AzureOpenAi:Endpoint is not configured.
        builder.Services.AddSingleton<IIssueTextRefinementService, RuleBasedIssueTextRefinementService>();
    }
}

// Categorization
builder.Services.AddSingleton<RuleBasedCategorizationService>();
if (useMockIntegrations)
{
    builder.Services.AddSingleton<ICategorizationService, MockCategorizationService>();
}
else
{
    var openAiEndpoint = builder.Configuration["AzureOpenAi:Endpoint"];
    var openAiApiKey = builder.Configuration["AzureOpenAi:ApiKey"];
    if (!string.IsNullOrWhiteSpace(openAiEndpoint))
    {
        var categorizationDeploymentName = builder.Configuration["AzureOpenAi:TextDeploymentName"]
            ?? builder.Configuration["AzureOpenAi:DeploymentName"]
            ?? "gpt-4o";

        builder.Services.AddHttpClient<ICategorizationService, AzureOpenAiCategorizationService>(client =>
        {
            var baseUrl = openAiEndpoint.TrimEnd('/') + $"/openai/deployments/{categorizationDeploymentName}/";
            client.BaseAddress = new Uri(baseUrl);
            if (!string.IsNullOrWhiteSpace(openAiApiKey))
            {
                client.DefaultRequestHeaders.Add("api-key", openAiApiKey);
            }
        })
        .AddStandardResilienceHandler(options =>
        {
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
            options.Retry.MaxRetryAttempts = 2;
        });
    }
    else
    {
        builder.Services.AddSingleton<ICategorizationService>(sp => sp.GetRequiredService<RuleBasedCategorizationService>());
    }
}

// Notifications (Email via ACS, SMS via ACS, Orchestrator)
if (useMockIntegrations)
{
    builder.Services.AddSingleton<INotificationService, NoOpNotificationService>();
    builder.Services.AddSingleton<ISmsNotificationService, NoOpSmsNotificationService>();
}
else
{
    var acsEndpoint = builder.Configuration["AzureCommunicationServices:Endpoint"];
    if (!string.IsNullOrWhiteSpace(acsEndpoint))
    {
        var credential = new DefaultAzureCredential();
        var acsUri = new Uri(acsEndpoint);

        builder.Services.AddSingleton(new Azure.Communication.Email.EmailClient(acsUri, credential));
        builder.Services.AddScoped<INotificationService, AcsEmailNotificationService>();

        builder.Services.AddSingleton(new Azure.Communication.Sms.SmsClient(acsUri, credential));
        builder.Services.AddScoped<ISmsNotificationService, AcsSmsNotificationService>();
    }
    else
    {
        builder.Services.AddSingleton<INotificationService, NoOpNotificationService>();
        builder.Services.AddSingleton<ISmsNotificationService, NoOpSmsNotificationService>();
    }
}
builder.Services.AddScoped<INotificationOrchestrator, NotificationOrchestrator>();

// Blob Storage
if (useMockIntegrations)
{
    builder.Services.AddSingleton<IBlobStorageService, MockBlobStorageService>();
}
else
{
    builder.Services.AddScoped<IBlobStorageService, BlobStorageService>();
}
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

// 1. Dev & Staging endpoints (OpenAPI, Swagger UI) — never exposed in Production
if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
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

// 2. HTTPS redirection (all Azure-hosted environments — Azure terminates SSL at the load balancer)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 3. CORS
app.UseCors("AllowBlazorClient");

// 4. Rate limiting
app.UseRateLimiter();

// 5. ExceptionHandlingMiddleware (IMiddleware, singleton)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 6. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 7. Structured logging — enriches log scope with tenantId, locationId, correlationId (after auth so claims are populated)
app.UseMiddleware<CorrelationLoggingMiddleware>();

// 8. Tenant access gate
app.UseMiddleware<TenantAccessGateMiddleware>();

// 9. Health endpoint (no auth required)
app.MapHealthChecks("/health");

// 10. Map controllers
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