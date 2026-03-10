# ASP.NET Core API — Patterns & Practices

> **Purpose:** This document captures the architectural conventions used in this API project. Reference it as a Copilot custom instruction or team style guide when scaffolding new controllers, services, mappers, middleware, or domain types.

---

## Table of Contents

1. [Solution & Project Structure](#1-solution--project-structure)
2. [Controller Patterns](#2-controller-patterns)
3. [Service Patterns](#3-service-patterns)
4. [Mapper Patterns](#4-mapper-patterns)
5. [Exception Handling Middleware](#5-exception-handling-middleware)
6. [Tenant Access Gate Middleware](#6-tenant-access-gate-middleware)
7. [Claims & Authorization Patterns](#7-claims--authorization-patterns)
8. [User Context Accessor](#8-user-context-accessor)
9. [Domain Interface Contracts](#9-domain-interface-contracts)
10. [Entity Base Classes & Auditing](#10-entity-base-classes--auditing)
11. [DTO Conventions](#11-dto-conventions)
12. [Validation Patterns](#12-validation-patterns)
13. [Dependency Registration](#13-dependency-registration)
14. [Middleware Pipeline Order](#14-middleware-pipeline-order)
15. [CORS Configuration](#15-cors-configuration)
16. [Resilience & HTTP Client Patterns](#16-resilience--http-client-patterns)

---

## 1. Solution & Project Structure

```
Solution
├── <App>.API            # ASP.NET Core Web API (controllers, services, mappers, middleware)
├── <App>.Domain          # Entities, DTOs, interfaces, validation — no infrastructure references
├── <App>.Infra.*         # Infrastructure implementations (Cosmos, Blob, Tables, etc.)
└── <App>.BlazorWASM      # Blazor WebAssembly front-end (optional)
```

### Key principles

| Principle | Detail |
|---|---|
| **Clean Architecture** | `Domain` has zero dependency on `API` or `Infra`. `API` references `Domain` and `Infra`. |
| **Focused services (SRP)** | When a single aggregate has multiple operational concerns, split into focused services rather than one monolithic service. |
| **Folder-per-concern** | `Controllers/`, `Services/`, `Mappers/`, `Middleware/`, `Integrations/` under the API project. |
| **No business logic in controllers** | Controllers delegate to services; services own validation and orchestration. |

---

## 2. Controller Patterns

### Structure

```csharp
[ApiController]
[Route("api/practices/{practiceId}/resources")]
[Authorize]
public class ResourcesController : ControllerBase
{
    private readonly IResourceService _resourceService;
    private readonly ClaimsService _claimsService;

    public ResourcesController(IResourceService resourceService, ClaimsService claimsService)
    {
        _resourceService = resourceService;
        _claimsService = claimsService;
    }
}
```

### Rules

1. **Always inherit `ControllerBase`** — no view support needed for pure APIs.
2. **Apply `[ApiController]`** at the class level for automatic model binding and `ProblemDetails` support.
3. **Apply `[Authorize]`** at the class level; opt out per-action with `[AllowAnonymous]` when needed.
4. **Route template** uses kebab-case nouns and nests child resources under parent IDs:
   - `api/practices/{practiceId}/patients`
   - `api/practices/{practiceId}/patients/{patientId}/encounters`
5. **Constructor injection** accepts only the focused service interface and `ClaimsService`.
6. **Every action** starts with tenant/practice resolution and access enforcement:
   ```csharp
   var tenantId = _claimsService.GetTenantIdOrThrow();
   _claimsService.EnsurePracticeAccessOrThrow(practiceId);
   ```
7. **Mapping** happens in the controller — the service returns domain entities; the controller maps to response DTOs.
8. **Return types:**
   - `GET` → `Ok(dto)`
   - `POST` (create) → `CreatedAtAction(nameof(GetAction), routeValues, dto)`
   - `PUT` → `Ok(dto)`
   - `DELETE` → `NoContent()`
9. **Search endpoints** use `[HttpPost("search")]` with a request DTO body (to support complex filter criteria).
10. **No `try/catch`** in controllers — let the `ExceptionHandlingMiddleware` convert exceptions to HTTP responses.

### Cross-controller references

When a create action in a child controller needs to return a `Location` header pointing to a parent controller's action:

```csharp
return CreatedAtAction(
    actionName: "GetParentResource",
    controllerName: "ParentResources",
    routeValues: new { parentId, practiceId },
    value: dto);
```

---

## 3. Service Patterns

### Structure

```csharp
public sealed class ResourceService : IResourceService
{
    private readonly IResourceRepository _repository;
    private readonly IUserContextAccessor _userContext;

    public ResourceService(IResourceRepository repository, IUserContextAccessor userContext)
    {
        _repository = repository;
        _userContext = userContext;
    }
}
```

### Rules

1. **Sealed classes** — services are `sealed` unless explicitly designed for inheritance.
2. **Constructor injection** accepts repository interfaces and `IUserContextAccessor` (not `HttpContext`).
3. **Guard clauses first** in every public method using `ArgumentException.ThrowIfNullOrWhiteSpace()` and `ArgumentNullException.ThrowIfNull()`.
4. **Business validation** follows guard clauses (e.g., checking paging params, required fields).
5. **Existence checks** — fetch the entity, throw `KeyNotFoundException` if null:
   ```csharp
   var entity = await _repository.GetByIdAsync(tenantId, practiceId, entityId);
   if (entity is null)
       throw new KeyNotFoundException("Resource not found.");
   ```
6. **Entity creation** uses mapper extension methods: `requestDto.ToEntity(tenantId, practiceId, _userContext.UserId)`.
7. **Entity updates** use `ApplyUpdate` extension methods: `entity.ApplyUpdate(request, _userContext.UserId)`.
8. **Return domain entities** — let the calling controller map to DTOs.
9. **Scoping** — all operations that touch tenant data require `tenantId` and `practiceId` parameters for data isolation.

---

## 4. Mapper Patterns

### Structure

```csharp
public static class ResourceMapper
{
    // Entity → DTO (Response)
    public static ResourceDetailResponseDto ToDetailDto(this ResourceEntity entity) { ... }
    public static ResourceSummaryResponseDto ToSummaryDto(this ResourceEntity entity) { ... }

    // DTO → Entity (Request)
    public static ResourceEntity ToEntity(this ResourceCreateRequestDto dto, string tenantId, string practiceId, string? createdByUserId = null) { ... }

    // DTO → Entity mutation (Update)
    public static void ApplyUpdate(this ResourceEntity entity, ResourceUpdateRequestDto dto, string? updatedByUserId = null) { ... }

    // Paged result mapping
    public static PagedResult<ResourceSummaryResponseDto> ToSummaryDto(this PagedResult<ResourceEntity> paged) { ... }
}
```

### Rules

1. **Static classes with extension methods** — one mapper class per aggregate/entity grouping.
2. **Null guards** at the top of every method: `ArgumentNullException.ThrowIfNull(entity)`.
3. **Two mapping directions:**
   - **Entity → DTO** (for responses): `ToDetailDto()`, `ToSummaryDto()`, `ToDto()`
   - **DTO → Entity** (for creates): `ToEntity(tenantId, practiceId, createdByUserId)`
   - **DTO → Entity mutation** (for updates): `ApplyUpdate(entity, dto, updatedByUserId)` — modifies in place, calls `entity.MarkAsUpdated(userId)`.
4. **Partial/patch updates** — only apply non-null DTO fields:
   ```csharp
   if (dto.Name is not null) entity.Name = dto.Name.Trim();
   if (dto.Date.HasValue) entity.Date = dto.Date;
   ```
5. **Trim string inputs** on create and update for name-like fields.
6. **`PagedResult<T>` mapping** — map items, carry forward `Page`, `PageSize`, `TotalCount`.
7. **No repository or service calls** inside mappers — they are pure data transforms.
8. **Section comments** separate Entity→DTO and DTO→Entity directions:
   ```csharp
   // =====================================================
   // Entity → DTO (Response) Mappings
   // =====================================================
   ```

---

## 5. Exception Handling Middleware

### Structure

Register as a singleton service implementing `IMiddleware`:

```csharp
public class ExceptionHandlingMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try { await next(context); }
        catch (Exception ex) { await HandleExceptionAsync(context, ex); }
    }
}
```

### Exception-to-status-code mapping

| Exception Type | HTTP Status Code | Client Message |
|---|---|---|
| `ArgumentException` | `400 Bad Request` | "The request was invalid." |
| `UnauthorizedAccessException` | `401 Unauthorized` | "Not authorized." |
| `KeyNotFoundException` | `404 Not Found` | "Resource not found." |
| *(all others)* | `500 Internal Server Error` | "A server error occurred. Please contact support with this ID." |

### Response body

```json
{
  "message": "<safe client message>",
  "errorId": "<guid>"
}
```

### Rules

1. **Generate a unique `errorId`** (GUID) per exception for support traceability.
2. **Log full detail** (exception, stack trace, tenant ID, user ID, request path) via `ILogger`.
3. **Never expose internal details to the client** — only `message` and `errorId` in production.
4. **Development mode** may include `exception` and `stackTrace` fields for debugging.
5. **Use `switch` on exception type** — extendable with additional domain exception types (e.g., `ConflictException`, `ValidationException`).
6. **Write JSON with `camelCase` naming policy.**
7. **Extract tenant/user context from claims** for structured log enrichment.

---

## 6. Tenant Access Gate Middleware

### Purpose

Intercepts requests after authentication to verify the tenant is enabled before allowing access to protected endpoints.

### Structure

```csharp
public sealed class TenantAccessGateMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly string[] AllowPrefixes = { "/api/tenants/config", "/health", "/swagger" };

    public async Task InvokeAsync(HttpContext ctx, IServiceScoped tenantService)
    {
        // 1. Skip allowlisted paths
        // 2. Skip unauthenticated requests (let auth middleware handle)
        // 3. Extract tenantId from claims
        // 4. Query tenant access gate
        // 5. Return 403 with structured error if disabled
        // 6. Call next(ctx) if enabled
    }
}
```

### Rules

1. **Uses `RequestDelegate` constructor injection** (standard middleware pattern, not `IMiddleware`).
2. **Scoped services** (e.g., tenant service) are injected into `InvokeAsync`, not the constructor.
3. **Allowlist** — certain paths bypass the gate (bootstrap config endpoints, health checks, Swagger).
4. **Unauthenticated requests pass through** — the `[Authorize]` attribute on controllers handles them.
5. **Tenant ID extraction** falls back across multiple claim types for compatibility.
6. **Returns `403 Forbidden`** with a structured JSON body including `error`, `reason`, `message`, and `support` fields.
7. **Runs before `UseAuthentication`/`UseAuthorization`** in the pipeline so it can short-circuit early after auth is resolved.

---

## 7. Claims & Authorization Patterns

### `ClaimsService`

A scoped service that provides centralized claim extraction and access enforcement:

```csharp
public sealed class ClaimsService
{
    public const string TenantIdClaimType = "http://example.com/tenantId";
    public const string PracticeIdClaimType = "http://example.com/practiceId";

    public string GetTenantIdOrThrow();
    public IReadOnlyCollection<string> GetPracticeIds();
    public void EnsurePracticeAccessOrThrow(string practiceId);
}
```

### Rules

1. **Claim type constants** are defined as `public const` on `ClaimsService` and reused wherever claims are read.
2. **`GetTenantIdOrThrow()`** — throws `UnauthorizedAccessException` if the tenant claim is missing.
3. **`EnsurePracticeAccessOrThrow(practiceId)`** — compares the requested practice against the token's practice claims; throws `UnauthorizedAccessException` on mismatch.
4. **Practice claims are multi-valued** — a user may have access to multiple practices.
5. **Comparison is case-insensitive** (`StringComparer.OrdinalIgnoreCase`).
6. **Every controller action** calls these methods before delegating to the service layer.

---

## 8. User Context Accessor

### Purpose

Provides a domain-layer abstraction over `HttpContext` claims so services can access the current user's identity without depending on ASP.NET Core types.

### Domain interface

```csharp
public interface IUserContextAccessor
{
    string? UserId { get; }
    string? TenantId { get; }
    IReadOnlyCollection<string> PracticeIds { get; }
}
```

### API implementation

```csharp
public sealed class HttpUserContextAccessor : IUserContextAccessor
{
    // Reads from IHttpContextAccessor, delegates to ClaimsService claim types
}
```

### Rules

1. **Domain defines the interface** — no ASP.NET Core references in the domain project.
2. **API implements it** using `IHttpContextAccessor`.
3. **Registered as scoped** alongside `IHttpContextAccessor`.
4. **Services use `_userContext.UserId`** for audit tracking on creates and updates.

---

## 9. Domain Interface Contracts

### Repository interfaces

```csharp
public interface IResourceRepository
{
    Task<PagedResult<Resource>> SearchAsync(string tenantId, string practiceId, SearchRequestDto request, CancellationToken ct = default);
    Task<Resource?> GetByIdAsync(string tenantId, string practiceId, string resourceId);
    Task CreateAsync(Resource entity);
    Task UpdateAsync(Resource entity);
}
```

### Service interfaces

```csharp
public interface IResourceService
{
    Task<PagedResult<Resource>> SearchAsync(string tenantId, string practiceId, SearchRequestDto request);
    Task<Resource> GetByIdAsync(string tenantId, string practiceId, string resourceId);
    Task<Resource> CreateAsync(string tenantId, string practiceId, CreateRequestDto request);
    Task<Resource> UpdateAsync(string tenantId, string practiceId, string resourceId, UpdateRequestDto request);
}
```

### Rules

1. **Interfaces live in `Domain/Interfaces/`** — no infrastructure types leak into contracts.
2. **Every data method requires `tenantId`** (and `practiceId` for practice-scoped data) for strict data isolation.
3. **Repository returns nullable** (`Resource?`) for single-item lookups — the service decides whether to throw.
4. **Service returns non-nullable** — it has already validated existence.
5. **`CancellationToken` support** on repository methods (optional parameter with default).
6. **XML doc comments** on every interface member.

---

## 10. Entity Base Classes & Auditing

### Hierarchy

```
EntityBase (abstract)
├── PracticeScopedEntityBase (abstract) — adds required PracticeId
└── (concrete entities)
```

### `EntityBase` properties

| Property | Type | Notes |
|---|---|---|
| `Type` | `abstract string` | Discriminator, `init`-only |
| `Id` | `string` | Auto-generated GUID, `init`-only |
| `TenantId` | `virtual string` | `init`-only, defaults to new GUID |
| `Name` | `virtual string` | Overridable display name |
| `IsEnabled` | `bool` | Soft-enable/disable flag |
| `CreatedAtUtc` | `DateTime` | Set once at creation, `init`-only |
| `UpdatedAtUtc` | `DateTime?` | Set on each update |
| `CreatedByUserId` | `string?` | `init`-only |
| `UpdatedByUserId` | `string?` | Set on each update |

### Rules

1. **`init`-only setters** for immutable identity fields (`Id`, `TenantId`, `CreatedAtUtc`, `CreatedByUserId`).
2. **`MarkAsUpdated(userId)`** — standard method to stamp `UpdatedAtUtc` and `UpdatedByUserId`.
3. **Embedded sub-entities** (e.g., collections within a document) follow the same audit property pattern but are not standalone documents.
4. **JSON serialization attributes** (`[JsonProperty("camelCase")]`) on all entity properties for Cosmos DB compatibility.

---

## 11. DTO Conventions

### Naming

| DTO Purpose | Suffix Pattern | Example |
|---|---|---|
| Create request | `CreateRequestDto` | `PatientCreateRequestDto` |
| Update request | `UpdateRequestDto` | `PatientUpdateRequestDto` |
| Search/filter request | `SearchRequestDto` | `PatientSearchRequestDto` |
| Detail response | `DetailResponseDto` | `PatientDetailResponseDto` |
| Summary response | `SummaryResponseDto` | `PatientSummaryResponseDto` |
| Search result response | `SearchResultResponseDto` | `PatientSearchResultResponseDto` |
| Paged wrapper | `PagedResult<T>` | `PagedResult<PatientSearchResultResponseDto>` |

### `PagedResult<T>`

```csharp
public sealed record PagedResult<T>
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public long TotalCount { get; init; }
    public List<T> Items { get; init; } = [];
    public string? ContinuationToken { get; init; }
}
```

### Rules

1. **DTOs live in `Domain/DTOs/`** — shared across API and any other consumers.
2. **Use `record` types** for DTOs when immutability is appropriate.
3. **Request DTOs** use `[FromBody]` in controller actions.
4. **Response DTOs** include audit fields (`CreatedAtUtc`, `UpdatedAtUtc`, etc.) when appropriate.
5. **No entity types** exposed in API responses — always map to DTOs.

---

## 12. Validation Patterns

### Guard-clause validation (service layer)

```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
ArgumentNullException.ThrowIfNull(request);

if (request.Page <= 0 || request.PageSize <= 0)
    throw new ArgumentException("Invalid paging parameters.", nameof(request));
```

### Domain validators (reusable)

```csharp
public static class SearchValidator
{
    public static ValidationResult Validate(string? name, int pageSize, string? continuationToken)
    {
        var errors = new List<string>();
        // Length checks, dangerous character checks, required-field checks
        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }
}
```

### Rules

1. **Guard clauses** in every service method for required parameters.
2. **Static validator classes** in `Domain/Validation/` for complex multi-field validation.
3. **Dangerous character screening** — block `<`, `>`, `;`, `'`, `"`, `\`, `\0` in search inputs.
4. **Max length enforcement** on all string inputs.
5. **Page size caps** (e.g., max 100) to prevent excessive resource consumption.
6. **Model state validation** is configured in `Program.cs` to return `422 Unprocessable Entity` for validation errors and `400 Bad Request` for unparsable input:
   ```csharp
   options.InvalidModelStateResponseFactory = actionContext =>
   {
       if (/* all keys parsed */)
           return new UnprocessableEntityObjectResult(actionContext.ModelState);
       return new BadRequestObjectResult(actionContext.ModelState);
   };
   ```

---

## 13. Dependency Registration

### Pattern

```csharp
// Middleware (singleton — IMiddleware pattern)
builder.Services.AddSingleton<ExceptionHandlingMiddleware>();

// Repositories (scoped — one per request, configured with connection details)
builder.Services.AddScoped<IResourceRepository>(sp =>
{
    var client = sp.GetRequiredService<DatabaseClient>();
    var databaseId = builder.Configuration["Database:Id"] ?? "defaultDb";
    return new ResourceRepository(client, databaseId, "containerName");
});

// Services (scoped)
builder.Services.AddScoped<IResourceService, ResourceService>();

// Cross-cutting (scoped)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IUserContextAccessor, HttpUserContextAccessor>();
builder.Services.AddScoped<ClaimsService>();
```

### Rules

1. **Repositories are scoped** with factory delegates that inject configuration.
2. **Services are scoped** with straightforward `AddScoped<TInterface, TImplementation>()`.
3. **Database clients are singletons** (thread-safe, connection-pooled).
4. **`IMiddleware` implementations are singletons** (registered as services, not via `UseMiddleware<T>` constructor injection).
5. **`RequestDelegate`-based middleware** (like the access gate) uses `UseMiddleware<T>()` and receives scoped services via `InvokeAsync` parameters.
6. **Group registrations** with `#region` blocks for clarity (repositories, services, integrations).

---

## 14. Middleware Pipeline Order

```csharp
// Development-only endpoints (OpenAPI, Swagger UI, mock tooling)
if (app.Environment.IsDevelopment()) { ... }

// HTTPS redirection (production only)
if (app.Environment.IsProduction())
    app.UseHttpsRedirection();

// CORS (environment-specific policy)
app.UseCors(app.Environment.IsProduction() ? "ProductionCors" : "DevelopmentCors");

// Global exception handling (wraps everything below)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Tenant access gate (after auth, before controllers)
// app.UseMiddleware<TenantAccessGateMiddleware>();

// Endpoint routing
app.MapControllers();
```

### Rules

1. **Exception handling middleware** runs early to catch exceptions from all downstream middleware.
2. **CORS** runs before auth so preflight requests are handled.
3. **Tenant access gate** runs after authentication so it can read claims, but before controller execution.

---

## 15. CORS Configuration

```csharp
// Production: Restricted to known origins
builder.Services.AddCors(options =>
{
    options.AddPolicy("ProductionCors", corsBuilder =>
    {
        corsBuilder
            .WithOrigins("https://portal.example.com")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// Development: Localhost origins for testing
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentCors", corsBuilder =>
    {
        corsBuilder
            .WithOrigins("https://localhost:7008", "http://localhost:5001")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
```

### Rules

1. **Named policies** — one per environment, selected at runtime.
2. **Never use `AllowAnyOrigin`** — always specify explicit origins.
3. **`AllowCredentials`** is required when the front-end sends cookies or bearer tokens.

---

## 16. Resilience & HTTP Client Patterns

For external HTTP integrations (clearinghouses, third-party APIs):

```csharp
builder.Services.AddHttpClient<IExternalClient, ExternalClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<IOptions<ExternalOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl);
    client.Timeout = Timeout.InfiniteTimeSpan; // Let resilience handler manage timeouts
})
.AddHttpMessageHandler<AuthHandler>()
.AddStandardResilienceHandler(options =>
{
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.BackoffType = DelayBackoffType.Exponential;
    options.Retry.UseJitter = true;
    options.Retry.Delay = TimeSpan.FromSeconds(1);
    options.Retry.MaxDelay = TimeSpan.FromSeconds(5);

    options.CircuitBreaker.MinimumThroughput = 2;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);
    options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(2);
});
```

### Rules

1. **Use `Microsoft.Extensions.Http.Resilience`** for retry, circuit breaker, and timeout policies.
2. **Delegating handlers** (e.g., `AuthHandler`) handle token acquisition/refresh for external APIs.
3. **Options pattern** (`IOptions<T>`) for external service configuration.
4. **Mock implementations** — register a mock client (`IExternalClient`) controlled by configuration for development/testing, preserving the same interface.

---

## Quick Reference: New Feature Checklist

When adding a new resource/entity to the API:

- [ ] **Domain:** Entity class inheriting `EntityBase` or `PracticeScopedEntityBase`
- [ ] **Domain:** DTOs — `CreateRequestDto`, `UpdateRequestDto`, `SearchRequestDto`, `DetailResponseDto`, `SummaryResponseDto`
- [ ] **Domain:** `IResourceService` interface in `Domain/Interfaces/`
- [ ] **Domain:** `IResourceRepository` interface in `Domain/Interfaces/`
- [ ] **Domain:** Validator in `Domain/Validation/` (if complex search/input rules)
- [ ] **Infra:** Repository implementation
- [ ] **API:** `ResourceMapper` static class in `Mappers/`
- [ ] **API:** `ResourceService` sealed class in `Services/`
- [ ] **API:** `ResourcesController` in `Controllers/`
- [ ] **API:** Register repository and service in `Program.cs`
