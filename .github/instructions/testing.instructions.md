---
applyTo: "Tests/**/*.cs"
---

# RVS Testing Guidelines — xUnit + Moq + FluentAssertions

## Stack
- **Framework**: xUnit v2 (net10.0, Microsoft.Testing.Platform)
- **Mocking**: Moq 4.x
- **Assertions**: FluentAssertions
- **Coverage**: coverlet.collector
- **Test Projects**: `Tests/RVS.Domain.Tests` (mappers, validators), `Tests/RVS.API.Tests` (services, middleware)

## TDD Cycle — Non-Negotiable Order
1. 🔴 RED — Write failing test in the correct project first. No implementation yet.
2. 🟢 GREEN — Write minimal implementation to pass the test. Do not over-engineer.
3. 🔵 REFACTOR — Clean up per `copilot-instructions.md` patterns. All tests must stay green.

## Test File Placement
- Mirror the source folder inside the test project:
  - `RVS.API/Services/PatientService.cs` → `Tests/RVS.API.Tests/Services/PatientServiceTests.cs`
  - `RVS.API/Mappers/PatientMapper.cs` → `Tests/RVS.Domain.Tests/Mappers/PatientMapperTests.cs`
- Shared test data lives in `Tests/RVS.API.Tests/Fakes/` and `Tests/RVS.Domain.Tests/Fakes/`

## Naming Convention
Use `MethodName_Scenario_ExpectedResult`:
- `GetAsync_WhenPatientExists_ShouldReturnPatient`
- `ToEntity_WhenRequestIsNull_ShouldThrowArgumentNullException`
- `ApplyUpdate_ShouldTrimFirstName_WhenLeadingSpacesPresent`

## Test Structure (AAA)
```csharp
[Fact]
public async Task MethodName_Scenario_ExpectedResult()
{
    // Arrange
    var mock = new Mock<IDependency>();
    mock.Setup(...).ReturnsAsync(...);
    var sut = new ServiceUnderTest(mock.Object);

    // Act
    var result = await sut.MethodAsync(...);

    // Assert
    result.Should().NotBeNull();
    result.SomeField.Should().Be(expectedValue);
}
```

## Layer Rules
- **Mapper/Validator tests** (`RVS.Domain.Tests`): zero mocks — pure inputs and outputs only
- **Service tests** (`RVS.API.Tests`): mock `IRepository` + `IUserContextAccessor` via Moq
- **Middleware tests** (`RVS.API.Tests`): use `DefaultHttpContext` directly — no hosting required
- **Integration tests**: use `WebApplicationFactory` + TestContainers for Cosmos DB

## Fakes Pattern
Create static `*Fakes` classes per entity in `Tests/*/Fakes/`:
```csharp
internal static class PatientFakes
{
    public static Patient ValidPatient(string id = "p1", string tenantId = "t1") => new() { ... };
    public static PatientCreateRequestDto ValidCreateRequest() => new() { ... };
}
```

## Guard Clause Tests — Always Include
Every service test class must include:
- Null/empty `tenantId` → `ArgumentException`
- Not-found entity → `KeyNotFoundException`
- Null request DTO → `ArgumentNullException`

## Forbidden
- No `try/catch` in tests — let exceptions propagate to xUnit
- No `Thread.Sleep` — use `async/await` throughout
- No `Assert.True(x == y)` — always use FluentAssertions
- No mocking concrete classes — only mock interfaces