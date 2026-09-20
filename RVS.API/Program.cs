using Azure.Core;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using RVS.API.HealthChecks;
using RVS.API.Integrations;
using RVS.API.Middleware;
using RVS.API.Packets;
using RVS.API.RateLimiting;
using RVS.API.Workers;
using Azure.Data.Tables;
using RVS.Infra.AzBlobRepository;
using RVS.Infra.AzTableRepository;
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
using Microsoft.ApplicationInsights.Extensibility;
using RVS.API.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Trace;


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

// Application Insights telemetry — only registered when a connection string is present.
// In Development this block is skipped — no connection string means no App Insights.
// In staging/production the connection string is injected by Bicep (app-service-config.bicep → APPLICATIONINSIGHTS_CONNECTION_STRING).
var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
    builder.Services.Configure<TelemetryConfiguration>(config =>
    {
        config.ConfigureOpenTelemetryBuilder(otel =>
        {
            otel.WithTracing(tracing =>
            {
                tracing.AddProcessor<TenantActivityProcessor>();
                tracing.AddProcessor<PiiFilterActivityProcessor>();
            });
        });
    });
}

// CORS — origins driven by Cors:AllowedOrigins (per-env appsettings).
// Bicep is the source of truth for prod/staging values; mirrored into appsettings.{Env}.json.
var corsAllowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? throw new InvalidOperationException("Cors:AllowedOrigins is not configured.");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", corsBuilder =>
    {
        corsBuilder
            .WithOrigins(corsAllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

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
    options.AddPolicy("CanCreateServiceRequests", policy =>
        policy.RequireClaim("permissions", "service-requests:create"));
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

    // Advisor intake invites (Spec A-14, issue #663) — send, list and read back in the send dialog
    options.AddPolicy("CanSendIntakeInvites", policy =>
        policy.RequireClaim("permissions", "intake-invites:send"));

    // Platform Admin — the permission AND a caller on Admin:AllowedUserIds (Spec P-7, issue #563)
    options.AddPolicy("PlatformAdmin", policy =>
        policy.RequireClaim("permissions", "platform:tenants:manage")
              .AddRequirements(new RVS.API.Authorization.PlatformAdminAllowlistRequirement()));
});

// The allowlist behind the PlatformAdmin policy. In Azure: Key Vault Admin--AllowedUserIds--0, --1, …
builder.Services.Configure<RVS.API.Options.AdminOptions>(
    builder.Configuration.GetSection(RVS.API.Options.AdminOptions.SectionName));
builder.Services.AddSingleton<
    Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    RVS.API.Authorization.PlatformAdminAllowlistHandler>();

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

// Rate limiting — protects the public intake + status endpoints. Partitioned per caller
// IP (Spec X-5) so a single abusive client cannot exhaust the fixed window for everyone;
// the client IP is read from X-Forwarded-For since the API sits behind Azure infra.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("StatusEndpoint", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpResolver.Resolve(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("IntakeEndpoint", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpResolver.Resolve(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1)
            }));

    // go.rvintake.com redirect (Spec A-13, issue #599). A looser window than intake on
    // purpose: this endpoint does no writing to Cosmos and is hit by link-preview fetchers as
    // well as customers, several per link composed, and an RV park's guests can share one
    // NATed address. Throttling here costs a customer their intake form.
    options.AddPolicy("RedirectEndpoint", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientIpResolver.Resolve(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 120,
                Window = TimeSpan.FromMinutes(1)
            }));
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

    // Gateway mode is set explicitly (the .NET SDK default is Direct). The API runs on
    // Azure App Service with public network access to Cosmos and no VNet integration;
    // Gateway keeps all traffic on 443, minimises the outbound TCP/SNAT footprint on the
    // small App Service SKU, and is the mode required if a dedicated gateway / integrated
    // cache is ever provisioned. Account-level consistency (Session) is configured in
    // modules/cosmos-db.bicep, not here.
    var options = new CosmosClientOptions { ConnectionMode = ConnectionMode.Gateway };

    return new CosmosClient(endpoint, key, options);
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

// Table Storage client — the append-only go.rvintake.com redirect hit log (Spec A-13,
// issue #599). Same storage account and same credential story as Blob: DefaultAzureCredential
// (managed identity) in Azure, AzureCliCredential locally to skip the managed-identity probe.
// Registered only when an endpoint is configured; without one the repository below falls back
// to the no-op and hits are dropped rather than the redirect failing.
var tableStorageEndpoint = builder.Configuration["TableStorage:Endpoint"];
if (!string.IsNullOrWhiteSpace(tableStorageEndpoint))
{
    builder.Services.AddSingleton<TableServiceClient>(sp =>
    {
        TokenCredential credential = builder.Environment.IsDevelopment()
            ? new AzureCliCredential()
            : new DefaultAzureCredential();

        return new TableServiceClient(new Uri(tableStorageEndpoint), credential);
    });
}

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

builder.Services.AddScoped<ITenantRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosTenantRepository>>();
    return new CosmosTenantRepository(client, cosmosDbId, logger);
});

builder.Services.AddScoped<IIntakeInviteRepository>(sp =>
{
    var client = sp.GetRequiredService<CosmosClient>();
    var logger = sp.GetRequiredService<ILogger<CosmosIntakeInviteRepository>>();
    return new CosmosIntakeInviteRepository(client, cosmosDbId, logger);
});

// go.rvintake.com redirect hits (Spec A-13, issue #599) — Azure Table Storage, not Cosmos.
// High-volume writes read occasionally, most of which never convert; Cosmos would charge
// request units on every machine-made link-preview fetch. Degrades to the no-op when no
// TableStorage:Endpoint is set, which is what a developer machine without a storage account
// gets: the redirect still works and the channel still reaches the service request, only the
// conversion denominator is missing.
if (!string.IsNullOrWhiteSpace(tableStorageEndpoint))
{
    builder.Services.AddScoped<IIntakeRedirectHitRepository>(sp =>
    {
        var client = sp.GetRequiredService<TableServiceClient>();
        var logger = sp.GetRequiredService<ILogger<AzTableIntakeRedirectHitRepository>>();
        return new AzTableIntakeRedirectHitRepository(client, logger);
    });
}
else
{
    builder.Services.AddScoped<IIntakeRedirectHitRepository, NoOpIntakeRedirectHitRepository>();
}
#endregion

#region Services
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IDealershipService, DealershipService>();
builder.Services.AddScoped<ILocationService, LocationService>();
builder.Services.AddScoped<ITenantConfigService, TenantConfigService>();
builder.Services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
builder.Services.AddScoped<ICustomerProfileService, CustomerProfileService>();
builder.Services.AddScoped<IGlobalCustomerAcctService, GlobalCustomerAcctService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();

builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IIntakeOrchestrationService, IntakeOrchestrationService>();
builder.Services.AddScoped<IIntakeRedirectService, IntakeRedirectService>();
builder.Services.AddScoped<IIntakeSourceReportService, IntakeSourceReportService>();
builder.Services.AddScoped<IIntakeInviteService, IntakeInviteService>();

// Advisor intake invites (Spec A-14, issue #663): expiry, the recent-sends window and the
// per-advisor/location/tenant caps. Configuration, not constants; bad values stop the app at startup.
builder.Services.AddOptions<RVS.API.Options.IntakeInviteOptions>()
    .Bind(builder.Configuration.GetSection(RVS.API.Options.IntakeInviteOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<IIntakeInviteRateLimiter, InMemoryIntakeInviteRateLimiter>();
// Inbound ACS SMS events over Event Grid (issue #665): carrier keywords and delivery reports.
// The webhook is anonymous, so the subscription's URL carries a shared secret; with no secret
// configured the endpoint refuses everything rather than accepting unauthenticated writes.
builder.Services.AddSingleton<IInboundSmsDeduplicator, InMemoryInboundSmsDeduplicator>();
builder.Services.AddScoped<IInboundSmsEventService, InboundSmsEventService>();
builder.Services.AddOptions<RVS.API.Options.EventGridInboundOptions>()
    .Bind(builder.Configuration.GetSection(RVS.API.Options.EventGridInboundOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddScoped<IPacketPhotoUrlResolver, PacketPhotoUrlResolver>();

// Packet generation (issue #434): non-blocking in-process queue + background worker.
// IPacketGenerationQueue is the seam for a future durable transport (e.g. Azure Storage Queue).
builder.Services.AddSingleton<IPacketGenerationQueue, ChannelPacketGenerationQueue>();
builder.Services.AddScoped<IPacketGenerationService, PacketGenerationService>();
builder.Services.AddHostedService<PacketGenerationWorker>();

// Packet-email delivery tuning (Spec B-4, issues #438, #521): retry backoff and the ACS size
// budget. Defaults work unset; an out-of-range MaxRequestBytes stops the app at startup.
builder.Services.AddOptions<RVS.API.Options.PacketEmailOptions>()
    .Bind(builder.Configuration.GetSection("PacketEmail"))
    .ValidateOnStart();
builder.Services.AddSingleton<
    Microsoft.Extensions.Options.IValidateOptions<RVS.API.Options.PacketEmailOptions>,
    RVS.API.Options.PacketEmailOptionsValidator>();
#endregion

#region Integration Clients
var useMockIntegrations = builder.Configuration.GetValue<bool>("Integrations:UseMocks");

// AI options — payload limits and allowed media types for all AI endpoints
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));

// Intake app URL — used to build QR-code / magic-link URLs pointing at the public Intake SPA
builder.Services.Configure<RVS.API.Options.IntakeUrlOptions>(builder.Configuration.GetSection("Intake"));

// Manager app URL — used to build deep links pointing at the authenticated Manager SPA
builder.Services.Configure<RVS.API.Options.ManagerAppUrlOptions>(builder.Configuration.GetSection("ManagerApp"));

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
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            // Recycle connections every 2 minutes so Azure OpenAI's server-side
            // idle-connection closes don't cause stale-socket errors on reuse.
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
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

// Preliminary assessment — packet pipeline, off the intake request thread (issue #507)
builder.Services.AddSingleton<RuleBasedPreliminaryAssessmentService>();
var assessmentEndpoint = builder.Configuration["AzureOpenAi:Endpoint"];
if (!useMockIntegrations && !string.IsNullOrWhiteSpace(assessmentEndpoint))
{
    // AssessmentDeploymentName lets the packet assessment use a different model than
    // categorization/refinement (issue #584) without any code change — blank it (Key
    // Vault secret always exists, empty when unset) to fall back to gpt-4o via
    // TextDeploymentName.
    var configuredAssessmentDeployment = builder.Configuration["AzureOpenAi:AssessmentDeploymentName"];
    var assessmentDeploymentName = !string.IsNullOrWhiteSpace(configuredAssessmentDeployment)
        ? configuredAssessmentDeployment
        : builder.Configuration["AzureOpenAi:TextDeploymentName"]
            ?? builder.Configuration["AzureOpenAi:DeploymentName"]
            ?? "gpt-4o";
    var assessmentApiKey = builder.Configuration["AzureOpenAi:ApiKey"];

    builder.Services.AddHttpClient<IPreliminaryAssessmentService, AzureOpenAiPreliminaryAssessmentService>(client =>
    {
        client.BaseAddress = new Uri(assessmentEndpoint.TrimEnd('/') + $"/openai/deployments/{assessmentDeploymentName}/");
        if (!string.IsNullOrWhiteSpace(assessmentApiKey))
        {
            client.DefaultRequestHeaders.Add("api-key", assessmentApiKey);
        }
    })
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(15);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(35);
        options.Retry.MaxRetryAttempts = 2;
    });
}
else
{
    builder.Services.AddSingleton<IPreliminaryAssessmentService>(sp => sp.GetRequiredService<RuleBasedPreliminaryAssessmentService>());
}

// Notifications (Email via ACS, SMS via ACS, Orchestrator)
// Development sends through staging's ACS resource as the az-login identity. Use
// AzureCliCredential directly, as Blob does, to skip DefaultAzureCredential's
// ManagedIdentityCredential probe timeout.
TokenCredential CreateAcsCredential() => builder.Environment.IsDevelopment()
    ? new AzureCliCredential()
    : new DefaultAzureCredential();

var acsEndpoint = builder.Configuration["AzureCommunicationServices:Endpoint"];
if (!useMockIntegrations && !string.IsNullOrWhiteSpace(acsEndpoint))
{
    builder.Services.AddSingleton(new Azure.Communication.Email.EmailClient(new Uri(acsEndpoint), CreateAcsCredential()));
    builder.Services.AddScoped<INotificationService, AcsEmailNotificationService>();
}
else
{
    builder.Services.AddSingleton<INotificationService, NoOpNotificationService>();
}

// SMS (issue #661): off unless AzureCommunicationServices:Sms:Enabled is true, checked before
// the endpoint — the endpoint is in every vault for email.
builder.Services.AddSmsNotifications(
    builder.Configuration,
    useMockIntegrations,
    acsUri => new Azure.Communication.Sms.SmsClient(acsUri, CreateAcsCredential()));
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

// Image transcoding (issue #508): HEIC/HEIF uploads -> JPEG on confirm so every packet
// consumer gets a universally-renderable raster. Defaults work unset.
builder.Services.Configure<RVS.API.Integrations.ImageTranscodeOptions>(builder.Configuration.GetSection("ImageTranscode"));
if (useMockIntegrations)
{
    builder.Services.AddSingleton<IImageTranscoder, NoOpImageTranscoder>();
}
else
{
    builder.Services.AddSingleton<IImageTranscoder, MagickImageTranscoder>();
}

// Identity provisioning — the platform-admin tool's Auth0 Management API client (Spec P-2/P-3/P-7,
// issue #563). Its own M2M application and its own config section: the Auth0Mgmt--* secrets in the
// staging vault belong to the rvs-config-automation app the Infra/Auth0 scripts use, and the API
// loads every vault secret. Deliberately no NoOp fallback — when unset, every call throws.
builder.Services.Configure<RVS.API.Options.Auth0ProvisionerOptions>(
    builder.Configuration.GetSection(RVS.API.Options.Auth0ProvisionerOptions.SectionName));
var auth0ProvisionerOptions = builder.Configuration
    .GetSection(RVS.API.Options.Auth0ProvisionerOptions.SectionName)
    .Get<RVS.API.Options.Auth0ProvisionerOptions>() ?? new RVS.API.Options.Auth0ProvisionerOptions();
if (auth0ProvisionerOptions.IsConfigured)
{
    builder.Services.AddSingleton<Auth0ManagementTokenCache>();
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddHttpClient<IIdentityProvisioner, Auth0ManagementProvisioner>(client =>
    {
        client.BaseAddress = auth0ProvisionerOptions.BaseUri;
    })
    // The default HttpClient loggers would record request URIs, and users-by-email carries the email.
    .RemoveAllLoggers()
    .AddStandardResilienceHandler(options =>
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(30);
        // Never replay a user create or role assignment; re-submitting the admin form is the retry.
        options.Retry.DisableForUnsafeHttpMethods();
    });
}
else
{
    builder.Services.AddSingleton<IIdentityProvisioner, UnconfiguredIdentityProvisioner>();
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
                            AuthorizationUrl = new Uri($"{context.ApplicationServices.GetRequiredService<IConfiguration>()["Auth0:Domain"]?.TrimEnd('/')}/authorize"),
                            TokenUrl = new Uri($"{context.ApplicationServices.GetRequiredService<IConfiguration>()["Auth0:Domain"]?.TrimEnd('/')}/oauth/token"),
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