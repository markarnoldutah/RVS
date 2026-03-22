# Copilot Instructions — ASP.NET Core API

## Project Structure

- `<App>.API` — Controllers, Services, Mappers, Middleware, Integrations
- `<App>.Domain` — Entities, DTOs, Interfaces, Validation (zero infra dependencies)
- `<App>.Infra.*` — Repository implementations (Cosmos, Blob, Tables)
- Target: .NET 10, C# 14, nullable enabled, implicit usings enabled

## Controllers

- Inherit `ControllerBase`. Apply `[ApiController]`, `[Authorize]` at class level.
- Routes use kebab-case nouns nested under parent IDs: `api/dealers/{dealerId}/resources`.
- Inject only the focused `IResourceService` and `ClaimsService`.
- Every action starts with:
  ```csharp
  var tenantId = _claimsService.GetTenantIdOrThrow();
  ```
- Service returns domain entities; controller maps to response DTOs.
- GET → `Ok(dto)`. POST create → `CreatedAtAction(nameof(GetAction), routeValues, dto)`. PUT → `Ok(dto)`. DELETE → `NoContent()`.
- Search endpoints use `[HttpPost("search")]` with a request DTO body.
- Never put `try/catch` in controllers — `ExceptionHandlingMiddleware` handles all exceptions.

## Services

- Always `sealed`. Inject repository interfaces and `IUserContextAccessor` (never `HttpContext`).
- Guard clauses first: `ArgumentException.ThrowIfNullOrWhiteSpace()`, `ArgumentNullException.ThrowIfNull()`.
- Existence checks: fetch entity, throw `KeyNotFoundException` if null.
- Create entities via `requestDto.ToEntity(tenantId, _userContext.UserId)`.
- Update entities via `entity.ApplyUpdate(request, _userContext.UserId)`.
- Return domain entities (not DTOs).
- All data operations require `tenantId` for tenant isolation.

## Mappers

- One `public static class` per aggregate in `Mappers/` using extension methods.
- Start every method with `ArgumentNullException.ThrowIfNull()`.
- Entity → DTO: `ToDetailDto()`, `ToSummaryDto()`, `ToDto()`.
- DTO → Entity (create): `ToEntity(tenantId, createdByUserId)`.
- DTO → Entity (update): `ApplyUpdate(entity, dto, updatedByUserId)` — mutates in place, calls `entity.MarkAsUpdated(userId)`.
- Partial updates: only apply non-null DTO fields. Trim name-like strings.
- `PagedResult<T>` mapping: map items, carry forward `Page`, `PageSize`, `TotalCount`.
- Pure data transforms only — no repository or service calls.

## Exception Handling

- `ExceptionHandlingMiddleware` implements `IMiddleware`, registered as singleton.
- Exception → HTTP mapping: `ArgumentException` → 400, `UnauthorizedAccessException` → 401, `KeyNotFoundException` → 404, everything else → 500.
- Response body: `{ "message": "<safe message>", "errorId": "<guid>" }`. Dev mode adds `exception` and `stackTrace`.
- Log full details (exception, tenantId, userId, path) via `ILogger`. Never expose internals to client.

## Tenant Access Gate Middleware

- Standard `RequestDelegate` middleware (not `IMiddleware`). Scoped services injected via `InvokeAsync`.
- Allowlists paths like `/api/tenants/config`, `/health`, `/swagger`.
- Passes unauthenticated requests through (auth middleware handles them).
- Extracts tenantId from claims, queries access gate, returns 403 with structured JSON if tenant disabled.

## Claims & Authorization

- `ClaimsService` (scoped) owns claim type constants and extraction methods.
- `GetTenantIdOrThrow()` → throws `UnauthorizedAccessException` if missing.
- Every controller action calls `GetTenantIdOrThrow()` before delegating to the service.

## User Context Accessor

- `IUserContextAccessor` interface in Domain: `UserId`, `TenantId`.
- `HttpUserContextAccessor` in API reads from `IHttpContextAccessor`. Registered scoped.
- Services use `_userContext.UserId` for audit fields — never depend on ASP.NET types directly.

## Domain Interfaces

- Repository: returns nullable for single lookups (`Resource?`), supports `CancellationToken`.
- Service: returns non-nullable (existence already validated). Every method requires `tenantId`.
- All interfaces in `Domain/Interfaces/` with XML doc comments.

## Entities

- Inherit `EntityBase` (abstract) 
- Identity fields use `init`-only setters: `Id`, `TenantId`, `CreatedAtUtc`, `CreatedByUserId`.
- `MarkAsUpdated(userId)` stamps `UpdatedAtUtc` and `UpdatedByUserId`.
- `[JsonProperty("camelCase")]` on all properties for Cosmos DB.
- Embedded sub-entities follow the same audit property pattern.

## DTOs

- Live in `Domain/DTOs/`. Use `record` types when appropriate.
- Naming: `{Entity}CreateRequestDto`, `{Entity}UpdateRequestDto`, `{Entity}SearchRequestDto`, `{Entity}DetailResponseDto`, `{Entity}SummaryResponseDto`, `{Entity}SearchResultResponseDto`.
- `PagedResult<T>` is a sealed record with `Page`, `PageSize`, `TotalCount`, `Items`, `ContinuationToken`.
- Never expose entity types in API responses.

## Validation

- Guard clauses in every service method for required parameters.
- Static validator classes in `Domain/Validation/` for complex multi-field rules.
- Block dangerous characters (`<`, `>`, `;`, `'`, `"`, `\`, `\0`) in search inputs.
- Enforce max lengths on strings and page size caps (max 100).
- Model state factory: 422 for validation errors, 400 for unparsable input.

## DI Registration

- Database clients: singleton. Repositories: scoped with factory delegates. Services: scoped.
- `IMiddleware` types: singleton. `RequestDelegate` middleware: use `UseMiddleware<T>()`.
- Register `IHttpContextAccessor`, `IUserContextAccessor`, `ClaimsService` as scoped.

## Middleware Pipeline Order

1. Dev-only endpoints (OpenAPI, Swagger UI)
2. HTTPS redirection (production only)
3. CORS (environment-specific named policy, never `AllowAnyOrigin`)
4. `ExceptionHandlingMiddleware`
5. Authentication & Authorization
6. Tenant access gate
7. `MapControllers()`

## HTTP Client Resilience

- Use `Microsoft.Extensions.Http.Resilience` with `AddStandardResilienceHandler`.
- Delegating handlers for token auth. `IOptions<T>` for config.
- Mock implementations behind the same interface for dev/testing.

## New Feature Checklist

When adding a new resource: create Entity (`EntityBase`), DTOs (Create/Update/Search request + Detail/Summary response), `IResourceService`, `IResourceRepository` in Domain; repository in Infra; `ResourceMapper`, `ResourceService` (sealed), `ResourcesController` in API; register in `Program.cs`.

## TDD — Test-Driven Development (Mandatory for All Issues)

All feature implementation MUST follow Red → Green → Refactor. This is not optional.

### Order of Implementation for Every Issue
1. **Domain mapper/validator tests first** (`Tests/RVS.Domain.Tests`) — zero dependencies, pure logic
2. **Service tests second** (`Tests/RVS.API.Tests/Services`) — mock `IRepository` + `IUserContextAccessor`
3. **Middleware/exception tests** if new exception types are introduced
4. **Implementation code** only after failing tests are written

### Tests Go Here
- `Tests/RVS.Domain.Tests/` — for anything in `RVS.Domain/` (mappers, validators, entities)
- `Tests/RVS.API.Tests/` — for anything in `RVS.API/` (services, middleware, controllers)

### Fakes Live Here
- `Tests/RVS.API.Tests/Fakes/` — entity and DTO builders used across API tests
- `Tests/RVS.Domain.Tests/Fakes/` — entity builders used across Domain tests

### Never Do These
- Never write production code without a failing test first
- Never modify a failing test to make it pass — fix the implementation
- Never skip the Refactor phase — guard clauses, trimming, and naming must comply with these instructions
