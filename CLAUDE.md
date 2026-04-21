# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Authoritative Docs

- Product / architecture source of truth: [Docs/ASOT/](Docs/ASOT/) — PRD, Technical PRD, implementation plan, Cosmos data model, Auth0 identity, Magic Link storage. Prefer these over any older notes elsewhere in the repo.
- Infra source of truth: Bicep files in [Docs/ASOT/Infra/](Docs/ASOT/Infra/). Do not trust hand-drawn diagrams or older docs for Azure resource configuration.
- Per-language instruction files also live in [.github/instructions/](.github/instructions/) (C#, ASP.NET, Blazor, Markdown, Testing).

## Solution Layout

Solution file is [RVS.slnx](RVS.slnx) (new SLNX format — `dotnet` CLI handles it; older `dotnet sln` subcommands may not).

| Project | Role |
| --- | --- |
| [RVS.API](RVS.API/) | ASP.NET Core 10 Web API. Controllers, Services, Mappers, Middleware, Integrations, Telemetry. Auth0 JWT Bearer, OpenAPI/Swagger. |
| [RVS.Domain](RVS.Domain/) | Entities, DTOs, Interfaces, Validation, Exceptions. **Zero infra dependencies.** |
| [RVS.Infra.AzCosmosRepository](RVS.Infra.AzCosmosRepository/) | Cosmos DB repository implementations. |
| [RVS.Infra.AzBlobRepository](RVS.Infra.AzBlobRepository/) | Azure Blob Storage (attachments). |
| [RVS.Infra.AzTablesRepository](RVS.Infra.AzTablesRepository/) | Azure Tables (tenant access gate). |
| [RVS.Infra.AzCredentials](RVS.Infra.AzCredentials/) | Shared credential helpers. |
| [RVS.Blazor.Intake](RVS.Blazor.Intake/) | Blazor **WASM** — anonymous 7-step customer intake wizard. |
| [RVS.Blazor.Manager](RVS.Blazor.Manager/) | Blazor **WASM** — authenticated dealer manager desktop (OIDC/Auth0, PKCE). |
| [RVS.UI.Shared](RVS.UI.Shared/) | Shared typed API clients (`ServiceRequestApiClient`, `AnalyticsApiClient`, `LookupApiClient`, `AttachmentApiClient`) + `ThemeService`. |
| [RVS.Data.Cosmos.Seed](RVS.Data.Cosmos.Seed/) | Idempotent seeder — creates 9 containers with partition keys/unique keys/indexing, seeds test data. |
| [Tests/RVS.Domain.Tests](Tests/RVS.Domain.Tests/) | Pure logic: mappers, validators, entities. |
| [Tests/RVS.API.Tests](Tests/RVS.API.Tests/) | Services, middleware, controllers (with Moq). |
| [Tests/RVS.UI.Shared.Tests](Tests/RVS.UI.Shared.Tests/) | Shared API client tests. |

Note: `RVS.UI.Shared` and `Tests/RVS.UI.Shared.Tests` are **excluded from Debug builds** in the solution — they are built only on demand.

## Common Commands

All commands run from repo root. Use bash (Git Bash / WSL) with forward slashes, or cmd with backslashes — paths in this doc use forward slashes.

```bash
# Build / restore / clean
dotnet restore RVS.slnx
dotnet build RVS.slnx --configuration Release

# Run all tests with coverage (matches CI)
dotnet test Tests/RVS.Domain.Tests/RVS.Domain.Tests.csproj     --configuration Release --collect:"XPlat Code Coverage"
dotnet test Tests/RVS.API.Tests/RVS.API.Tests.csproj           --configuration Release --collect:"XPlat Code Coverage"
dotnet test Tests/RVS.UI.Shared.Tests/RVS.UI.Shared.Tests.csproj --configuration Release --collect:"XPlat Code Coverage"

# Run a single test class or test
dotnet test Tests/RVS.API.Tests/RVS.API.Tests.csproj --filter "FullyQualifiedName~ServiceRequestServiceTests"
dotnet test Tests/RVS.API.Tests/RVS.API.Tests.csproj --filter "FullyQualifiedName=RVS.API.Tests.Services.ServiceRequestServiceTests.GetAsync_WhenNotFound_ShouldThrow"

# Run individual apps (each has launchSettings.json with HTTPS profile)
dotnet run --project RVS.API                -lp https   # https://localhost:7116
dotnet run --project RVS.Blazor.Intake      -lp https   # https://localhost:7200
dotnet run --project RVS.Blazor.Manager     -lp https   # https://localhost:7300

# Launch API + Intake + Manager + (Cosmos Seed shell) + MudMCP in Windows Terminal tabs
Tools/rvs-launch.cmd

# Seed Cosmos — idempotent; re-run is safe
dotnet run --project RVS.Data.Cosmos.Seed                     # Local (emulator)
dotnet run --project RVS.Data.Cosmos.Seed -- --environment Staging
```

WASM workload is required for Blazor projects. CI installs it via `dotnet workload install wasm-tools`; do the same locally if Blazor builds fail.

## Architecture at a Glance

**Multi-tenant B2B SaaS for RV dealerships.** Tenant = corporation, partitioned by `tenantId` (= Auth0 `org_id`). Three client apps share one API.

### Request → Response Flow

1. **Intake WASM** (anonymous, rate-limited) and **Manager WASM** (Auth0 OIDC + bearer token) call **RVS.API**.
2. API middleware pipeline (order is load-bearing — see [RVS.API/Program.cs](RVS.API/Program.cs)):
   - CORS (`AllowBlazorClient`, environment-specific origins — never `AllowAnyOrigin`)
   - Rate limiter (`IntakeEndpoint`, `StatusEndpoint`)
   - `ExceptionHandlingMiddleware` (`IMiddleware`, singleton — maps exceptions to status codes, returns `{ message, errorId }`)
   - Authentication → Authorization (Auth0 JWT, `permissions` claim policies)
   - `CorrelationLoggingMiddleware` (enriches log scope with tenantId/locationId/correlationId)
   - `TenantAccessGateMiddleware` (allowlists `/health`, `/swagger`, `/api/tenants/config`; otherwise checks tenant disabled flag → 403)
   - `MapControllers`
3. **Controllers** (`[ApiController]`, `[Authorize]`, kebab-case nested routes): every action starts with `_claimsService.GetTenantIdOrThrow()`, delegates to sealed service, maps entity → DTO. **No try/catch in controllers.**
4. **Services** (sealed, scoped): guard clauses → repository → return domain entities.
5. **Repositories** (scoped, Cosmos): all queries are **single-partition on `tenantId`** — cross-partition is structurally prevented.

### Cosmos DB (9 containers)

`serviceRequests`, `customerProfiles`, `globalCustomerAccts`, `assetLedger`, `dealerships`, `locations`, `tenantConfigs`, `lookupSets`, `slugLookup`. `ConnectionMode.Gateway` (server-side caching enabled). Container creation + seeding lives in [RVS.Data.Cosmos.Seed/Program.cs](RVS.Data.Cosmos.Seed/Program.cs).

### Auth & Claims

- Auth0 JWT Bearer, audience `https://api.rvserviceflow.com`.
- Authorization policies are **per-permission**, not per-role — e.g. `CanReadServiceRequests` requires claim `permissions:service-requests:read`. Full list in [RVS.API/Program.cs](RVS.API/Program.cs).
- `ClaimsService` (scoped) owns claim constants and extraction. `GetTenantIdOrThrow()` throws `UnauthorizedAccessException` if missing.
- `IUserContextAccessor` / `HttpUserContextAccessor` isolates services from `HttpContext` — services use `_userContext.UserId` for audit fields.

### AI / External Integrations

All integrations have a `Mock*`/`NoOp*` fallback behind the same interface — toggled by `Integrations:UseMocks` in config. Real implementations use `Microsoft.Extensions.Http.Resilience.AddStandardResilienceHandler` with per-client timeouts:

- VIN Decode → NHTSA vPIC (`NhtsaVinDecoderClient`)
- VIN Extraction (vision) → Azure OpenAI (`AzureOpenAiVinExtractionService`)
- Speech-to-Text → Azure OpenAI Whisper (**northcentralus** — Whisper 001 Standard not in westus3)
- Issue text refinement + categorization → Azure OpenAI (fallback: `RuleBasedIssueTextRefinementService` / `RuleBasedCategorizationService`)
- Email + SMS → Azure Communication Services (fallback: NoOp)

### Secrets Model

- **Development**: `appsettings.Development.json` + `dotnet user-secrets`.
- **Staging/Production**: Azure Key Vault, loaded at startup when `KeyVault:VaultUri` is set (injected by Bicep via `KeyVault__VaultUri`). Uses `DefaultAzureCredential` (Managed Identity in Azure).
- Blob Storage in dev uses `AzureCliCredential` directly (avoids the ~15 s `ManagedIdentityCredential` probe timeout from `DefaultAzureCredential`).

### Telemetry

Application Insights only registers when `APPLICATIONINSIGHTS_CONNECTION_STRING` is present (injected by Bicep in staging/prod; absent in dev = no telemetry). Custom OpenTelemetry processors: `TenantActivityProcessor`, `PiiFilterActivityProcessor`.

## Coding Patterns (Backend)

Target: .NET 10, C# 14, nullable enabled, implicit usings enabled.

### Controllers

- Inherit `ControllerBase`; apply `[ApiController]` and `[Authorize]` at class level.
- Routes are kebab-case nouns nested under parent IDs: `api/dealers/{dealerId}/resources`.
- Inject only the focused `IResourceService` and `ClaimsService` — nothing else.
- Every action starts with `var tenantId = _claimsService.GetTenantIdOrThrow();`.
- Service returns domain entities; controller maps to response DTOs.
- Returns: `Ok(dto)` for GET/PUT; `CreatedAtAction(nameof(GetAction), routeValues, dto)` for POST create; `NoContent()` for DELETE.
- Search endpoints use `[HttpPost("search")]` with a request DTO body.
- **Never put `try/catch` in controllers.** `ExceptionHandlingMiddleware` handles all exceptions.

### Services

- Always `sealed` and scoped. Inject repository interfaces and `IUserContextAccessor` — never `HttpContext`.
- Guard clauses first: `ArgumentException.ThrowIfNullOrWhiteSpace()`, `ArgumentNullException.ThrowIfNull()`.
- Existence checks: fetch entity, throw `KeyNotFoundException` if null.
- Create via `requestDto.ToEntity(tenantId, _userContext.UserId)`; update via `entity.ApplyUpdate(request, _userContext.UserId)`.
- Return domain entities (not DTOs). Every data operation requires `tenantId`.

### Mappers

- One `public static class` per aggregate in `Mappers/` using extension methods.
- Every method starts with `ArgumentNullException.ThrowIfNull()`.
- Entity → DTO: `ToDetailDto()`, `ToSummaryDto()`, `ToDto()`.
- DTO → Entity (create): `ToEntity(tenantId, createdByUserId)`.
- DTO → Entity (update): `ApplyUpdate(entity, dto, updatedByUserId)` — mutates in place, then calls `entity.MarkAsUpdated(userId)`.
- Partial updates: only apply non-null DTO fields. Trim name-like strings.
- `PagedResult<T>` mapping: map items, carry forward `Page`, `PageSize`, `TotalCount`.
- **Pure data transforms only** — no repository or service calls.

### Entities

- Inherit abstract `EntityBase`.
- Identity fields use `init`-only setters: `Id`, `TenantId`, `CreatedAtUtc`, `CreatedByUserId`.
- `MarkAsUpdated(userId)` stamps `UpdatedAtUtc` and `UpdatedByUserId`.
- `[JsonProperty("camelCase")]` on all properties (Cosmos serialization).
- Embedded sub-entities follow the same audit property pattern.

### DTOs

- Live in `Domain/DTOs/`. Use `record` types when appropriate.
- Naming: `{Entity}CreateRequestDto`, `{Entity}UpdateRequestDto`, `{Entity}SearchRequestDto`, `{Entity}DetailResponseDto`, `{Entity}SummaryResponseDto`, `{Entity}SearchResultResponseDto`.
- `PagedResult<T>` is a sealed record with `Page`, `PageSize`, `TotalCount`, `Items`, `ContinuationToken`.
- **Never expose entity types in API responses.**

### Domain Interfaces

- Repository methods: return nullable for single lookups (`Resource?`), always support `CancellationToken`.
- Service methods: return non-nullable (existence already validated). Every method requires `tenantId`.
- All interfaces live in `Domain/Interfaces/` with XML doc comments.

### Validation

- Guard clauses in every service method for required parameters.
- Static validator classes in `Domain/Validation/` for complex multi-field rules.
- Block dangerous characters (`<`, `>`, `;`, `'`, `"`, `\`, `\0`) in search inputs.
- Enforce max string lengths; page size cap is **100**.
- Model state factory (configured in `Program.cs`): **422** for validation errors, **400** for unparsable input.

### Exception Handling Middleware

- `ExceptionHandlingMiddleware` implements `IMiddleware`, registered singleton.
- Exception → HTTP mapping: `ArgumentException` → **400**, `UnauthorizedAccessException` → **401**, `KeyNotFoundException` → **404**, everything else → **500**.
- Response body: `{ "message": "<safe message>", "errorId": "<guid>" }`. Dev mode adds `exception` and `stackTrace`.
- Log full details (exception, tenantId, userId, path) via `ILogger`. Never expose internals to client.

### Tenant Access Gate Middleware

- Standard `RequestDelegate` middleware (not `IMiddleware`). Scoped services injected via `InvokeAsync`.
- Allowlists `/api/tenants/config`, `/health`, `/swagger`, and similar infra paths.
- Passes unauthenticated requests through (auth middleware handles them).
- Extracts tenantId from claims, queries the access gate, returns **403** with structured JSON if the tenant is disabled.

### Claims & User Context

- `ClaimsService` (scoped) owns claim-type constants and extraction. `GetTenantIdOrThrow()` throws `UnauthorizedAccessException` when missing.
- `IUserContextAccessor` lives in Domain (`UserId`, `TenantId`). `HttpUserContextAccessor` in API reads from `IHttpContextAccessor`, registered scoped.
- Services always read audit identity from `_userContext.UserId` — never depend on ASP.NET types directly.

### DI Registration Rules

- Database clients (Cosmos, Blob, Tables): **singleton**.
- Repositories: **scoped** via factory delegates.
- Services: **scoped**.
- `IMiddleware` types: **singleton**. `RequestDelegate` middleware: register via `UseMiddleware<T>()`.
- `IHttpContextAccessor`, `IUserContextAccessor`, `ClaimsService`: **scoped**.

### Middleware Pipeline Order

Canonical order (see [RVS.API/Program.cs](RVS.API/Program.cs)):

1. Dev-only endpoints (OpenAPI, Swagger UI)
2. HTTPS redirection (non-dev only)
3. CORS — environment-specific named policy, **never** `AllowAnyOrigin`
4. Rate limiter
5. `ExceptionHandlingMiddleware`
6. Authentication → Authorization
7. `CorrelationLoggingMiddleware` (after auth so claims are populated)
8. `TenantAccessGateMiddleware`
9. Health endpoint (no auth)
10. `MapControllers()`

### HTTP Client Resilience

- Use `Microsoft.Extensions.Http.Resilience.AddStandardResilienceHandler` for every external client.
- Delegating handlers for token auth. `IOptions<T>` for config.
- Every integration has a mock implementation behind the same interface for dev/testing — toggled by `Integrations:UseMocks`.

### New-Feature Checklist

When adding a new resource:

1. Entity (inherits `EntityBase`) + DTOs (Create/Update/Search request + Detail/Summary response) + `IResourceService` + `IResourceRepository` in **Domain**.
2. Repository implementation in the appropriate **Infra** project.
3. `ResourceMapper`, sealed `ResourceService`, `ResourcesController` in **API**.
4. Register services + repository in `Program.cs`.
5. Tests written **first** per TDD order below.

## Testing — TDD Is Mandatory

Full rules in [.github/instructions/testing.instructions.md](.github/instructions/testing.instructions.md). Stack: xUnit v2 (net10.0, Microsoft.Testing.Platform) + Moq + FluentAssertions + coverlet.

**Red → Green → Refactor. Non-negotiable order for every issue:**

1. Domain mapper/validator tests first ([Tests/RVS.Domain.Tests/](Tests/RVS.Domain.Tests/)) — zero dependencies, pure logic.
2. Service tests second ([Tests/RVS.API.Tests/Services/](Tests/RVS.API.Tests/)) — mock `IRepository` + `IUserContextAccessor`.
3. Middleware/exception tests if new exception types are introduced.
4. Implementation code **only** after failing tests exist.

Mirror source folders under the matching test project (e.g. `RVS.API/Services/Foo.cs` → `Tests/RVS.API.Tests/Services/FooTests.cs`). Shared builders live in `Tests/*/Fakes/`.

Naming: `MethodName_Scenario_ExpectedResult`. Every service test class must cover: null/empty `tenantId` → `ArgumentException`; not-found entity → `KeyNotFoundException`; null request DTO → `ArgumentNullException`.

**Never:** write production code before a failing test; modify a failing test to make it pass; skip Refactor; use `try/catch` or `Thread.Sleep` in tests; `Assert.True(x == y)` instead of FluentAssertions; mock concrete classes (interfaces only).

## Frontend — MudBlazor 9.x

All Blazor projects use **MudBlazor 9.x** (Material Design 3). **Do not** use `Microsoft.FluentUI.AspNetCore.Components`.

### Setup per project

- `wwwroot/index.html`: link `MudBlazor.min.css`, Roboto font, `MudBlazor.min.js`.
- `Program.cs`: `builder.Services.AddMudServices()`.
- `_Imports.razor`: `@using MudBlazor`.
- Root layout wraps body in `<MudThemeProvider>`, `<MudPopoverProvider>`, `<MudDialogProvider>`, `<MudSnackbarProvider>`.

### Theme

Single `MudTheme` defined in `MainLayout.razor` with `PaletteLight` (Primary `#1565C0`, Secondary `#00897B`). Apply via `<MudThemeProvider Theme="_theme" />`. Never inline ad-hoc colors.

### Component Conventions

- Layout: `MudLayout` → `MudAppBar` → `MudMainContent`.
- Typography: `<MudText Typo="Typo.h3">` for headings (not raw `<h3>`).
- Surfaces: `<MudPaper Elevation="2">` for containers; `<MudCard>` / `<MudCardContent>` for content cards.
- Stacks: `<MudStack Row="true" AlignItems="AlignItems.Center" Spacing="2">`.
- Buttons: `Variant.Filled` + `Color.Primary` for primary CTA; `Variant.Outlined` for secondary; `Variant.Text` for tertiary.
- Text inputs: always `Variant.Outlined` with `Label=` for floating labels; use `Adornment` for icons.
- Alerts: `<MudAlert Severity="Severity.Error|Warning|Info|Success">`.
- Icons: `@Icons.Material.Filled.*` / `@Icons.Material.Outlined.*`.

### MudBlazor v9 Breaking-Change Landmines

- `MudExpansionPanel.IsInitiallyExpanded` → **`Expanded`**.
- `MudIconButton.Title` → **`aria-label`** attribute.
- `MudFileUpload` activator/button template fragments **removed** — overlay a hidden `<InputFile>` absolutely on a dashed `<MudPaper>` drop zone.
- `MudFileUpload.InputStyle` is obsolete — remove it.
- Complex C# expressions in `Style="..."` Razor attributes produce **RZ9986** — extract to a helper method in `@code`.

## CI/CD

Three workflows in [.github/workflows](.github/workflows/) — detailed runbook in [.github/workflows/README.md](.github/workflows/README.md):

- `build-test.yml` — PR gate, runs on every push/PR. Never deploys.
- `deploy-staging.yml` — runs on push to `main`. **Only place that builds deployable artifacts.**
- `deploy-production.yml` — promotes the exact artifacts staging validated. **Never rebuilds from source.**

Auth: API uses Azure OIDC (federated credentials, no long-lived secrets). Static Web Apps use long-lived deployment tokens stored as environment secrets.
