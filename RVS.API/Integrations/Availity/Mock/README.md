# Availity API Mock Solution

This document describes the mock implementation for the Availity Eligibility/Coverages API, enabling easy testing without connecting to the real Availity service.

## Overview

The mock solution provides:

1. **MockAvailityEligibilityClient** - Drop-in replacement for the real client
2. **MockAvailityScenarios** - Predefined test scenarios matching Availity's demo patterns
3. **MockScenarioMiddleware** - HTTP header-based scenario selection for integration tests
4. **DI Extensions** - Easy service registration with configuration-based switching

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Test / Development Request                                     │
│  Header: X-Api-Mock-Scenario-ID: Coverages-Complete-i           │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  MockScenarioMiddleware                                         │
│  - Reads scenario header                                        │
│  - Configures MockAvailityEligibilityClient                     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  EligibilityCheckService                                        │
│  - Uses IAvailityEligibilityClient (mock or real)               │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  MockAvailityEligibilityClient                                  │
│  - Returns canned responses based on scenario                   │
│  - Tracks poll counts for async simulation                      │
└─────────────────────────────────────────────────────────────────┘
```

## Files

| File | Purpose |
|------|---------|
| `MockAvailityScenarios.cs` | Constants for all mock scenario IDs |
| `MockAvailityEligibilityClient.cs` | Mock client implementation |
| `MockScenarioMiddleware.cs` | HTTP header-based scenario selection |
| `AvailityServiceCollectionExtensions.cs` | DI registration helpers |

## Configuration

### appsettings.Development.json

```json
{
  "Availity": {
    "BaseUrl": "https://api.availity.com",
    "EligibilityPath": "/availity/v1/coverages",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "TokenUrl": "https://api.availity.com/v1/token"
  },
  "AvailityMock": {
    "UseMock": true,
    "DefaultScenario": "Coverages-Complete-i",
    "SimulatedDelayMs": 100
  }
}
```

### Program.cs Registration

```csharp
using BF.API.Integrations.Availity.Mock;

// Option 1: Configuration-based (auto-switches based on UseMock)
builder.Services.AddAvailityEligibilityClient(builder.Configuration);

// Option 2: Always use mock (for test projects)
builder.Services.AddMockAvailityEligibilityClient(options =>
{
    options.DefaultScenario = MockAvailityScenarios.CoveragesComplete;
    options.SimulatedDelayMs = 0;
});

// Add middleware for header-based scenario selection (development only)
if (app.Environment.IsDevelopment())
{
    app.UseMockScenarioMiddleware();
}
```

## Available Scenarios

### Basic Scenarios

| Scenario ID | Status Code | Description |
|-------------|-------------|-------------|
| `Coverages-Complete-i` | 4 | Immediate success with full benefits |
| `Coverages-InProgress-i` | 0 | In progress (polling required) |
| `Coverages-Retrying-i` | R1 | Payer retrying |
| `Coverages-PayerError1-i` | 19 | Provider ineligible |
| `Coverages-PayerError2-i` | 19 | Invalid subscriber name |
| `Coverages-RequestError1-i` | 400 | Validation failed |
| `Coverages-CommunicationError-i` | 7 | Payer timeout |
| `Coverages-PartialResponse-i` | 3 | Complete with partial/invalid data |

### Polling Simulation Scenarios

| Scenario ID | Behavior |
|-------------|----------|
| `Coverages-Polling-Success-i` | InProgress x2, then Complete |
| `Coverages-Polling-Failure-i` | InProgress x3, then Payer Error |
| `Coverages-Polling-Timeout-i` | Always InProgress (tests max polls) |

### Coverage Type Scenarios

| Scenario ID | Coverage Type |
|-------------|---------------|
| `Coverages-Vision-i` | Vision with frames/lenses/exam |
| `Coverages-Medical-HDHP-i` | High deductible health plan |
| `Coverages-Dental-i` | Dental with preventive/basic/major |
| `Coverages-Medicare-i` | Medicare Advantage |
| `Coverages-Medicaid-i` | Medicaid |
| `Coverages-COB-Primary-i` | Primary coverage |
| `Coverages-COB-Secondary-i` | Secondary coverage |

## Usage Patterns

### Unit Test - Direct Instantiation

```csharp
using BF.API.Integrations.Availity.Mock;
using BF.Domain.Integrations.Availity;

[Fact]
public async Task RunEligibilityCheck_ReturnsComplete_WhenPayerResponds()
{
    // Arrange
    var mockClient = new MockAvailityEligibilityClient
    {
        Scenario = MockAvailityScenarios.CoveragesComplete,
        SimulatedDelayMs = 0  // Fast tests
    };
    
    var request = new AvailityEligibilityRequest
    {
        PayerId = "BCBS",
        MemberId = "ABC123456",
        DateOfService = DateTime.Today
    };
    
    // Act
    var response = await mockClient.InitiateCoverageCheckAsync(request, CancellationToken.None);
    
    // Assert
    Assert.Equal("4", response.StatusCode);
    Assert.NotEmpty(response.CoverageId);
}
```

### Unit Test - Async Polling Lifecycle

```csharp
[Fact]
public async Task PollEligibilityCheck_ReturnsComplete_AfterMultiplePolls()
{
    // Arrange
    var mockClient = new MockAvailityEligibilityClient
    {
        Scenario = MockAvailityScenarios.CoveragesPollingSuccess,
        SimulatedDelayMs = 0
    };
    
    var request = new AvailityEligibilityRequest
    {
        PayerId = "BCBS",
        MemberId = "ABC123456",
        DateOfService = DateTime.Today
    };
    
    // Act - Initiate (should return InProgress)
    var initiate = await mockClient.InitiateCoverageCheckAsync(request, CancellationToken.None);
    Assert.Equal("0", initiate.StatusCode);
    
    // Act - Poll until complete
    AvailityPollResponse poll;
    int pollCount = 0;
    
    do
    {
        poll = await mockClient.PollCoverageStatusAsync(initiate.CoverageId, CancellationToken.None);
        pollCount++;
    } while (poll.IsProcessing && pollCount < 10);
    
    // Assert
    Assert.Equal(3, pollCount);  // 2 InProgress + 1 Complete
    Assert.True(poll.IsComplete);
    Assert.NotNull(poll.Result);
    Assert.NotEmpty(poll.Result.CoverageLines);
}
```

### Unit Test - Error Scenario

```csharp
[Fact]
public async Task InitiateCheck_ReturnsPayerError_WhenProviderIneligible()
{
    // Arrange
    var mockClient = new MockAvailityEligibilityClient
    {
        Scenario = MockAvailityScenarios.CoveragesPayerError1,
        SimulatedDelayMs = 0
    };
    
    var request = new AvailityEligibilityRequest
    {
        PayerId = "BCBS",
        MemberId = "ABC123456",
        DateOfService = DateTime.Today
    };
    
    // Act
    var response = await mockClient.InitiateCoverageCheckAsync(request, CancellationToken.None);
    
    // Assert
    Assert.Equal("19", response.StatusCode);
    Assert.NotNull(response.ValidationMessages);
    Assert.Contains("ineligible", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
}
```

### Unit Test - Switching Scenarios

```csharp
[Fact]
public async Task MockClient_CanSwitchScenarios_BetweenTests()
{
    var mockClient = new MockAvailityEligibilityClient { SimulatedDelayMs = 0 };
    var request = new AvailityEligibilityRequest
    {
        PayerId = "BCBS",
        MemberId = "ABC123456",
        DateOfService = DateTime.Today
    };
    
    // Test 1: Success
    mockClient.Scenario = MockAvailityScenarios.CoveragesComplete;
    var success = await mockClient.InitiateCoverageCheckAsync(request, CancellationToken.None);
    Assert.Equal("4", success.StatusCode);
    
    // Reset and Test 2: Error
    mockClient.Reset();
    mockClient.Scenario = MockAvailityScenarios.CoveragesCommunicationError;
    var error = await mockClient.InitiateCoverageCheckAsync(request, CancellationToken.None);
    Assert.Equal("7", error.StatusCode);
}
```

### Integration Test - HTTP Header

```bash
# Initiate eligibility check with specific scenario
curl -X POST "https://localhost:5001/api/practices/p1/encounters/e1/eligibility-checks" \
  -H "Authorization: Bearer $TOKEN" \
  -H "X-Api-Mock-Scenario-ID: Coverages-InProgress-i" \
  -H "Content-Type: application/json" \
  -d '{"coverageEnrollmentId": "cov1"}'

# Response includes header: X-Api-Mock-Response: true
```

### Integration Test - WebApplicationFactory

```csharp
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public string MockScenario { get; set; } = MockAvailityScenarios.CoveragesComplete;
    
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove real client
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IAvailityEligibilityClient));
            if (descriptor != null)
                services.Remove(descriptor);
            
            // Add mock client
            services.AddMockAvailityEligibilityClient(options =>
            {
                options.DefaultScenario = MockScenario;
                options.SimulatedDelayMs = 0;
            });
        });
    }
}

[Fact]
public async Task EligibilityCheck_ReturnsComplete_WhenPayerResponds()
{
    await using var factory = new CustomWebApplicationFactory
    {
        MockScenario = MockAvailityScenarios.CoveragesComplete
    };
    
    var client = factory.CreateClient();
    
    var response = await client.PostAsJsonAsync(
        "/api/practices/p1/encounters/e1/eligibility-checks",
        new { coverageEnrollmentId = "cov1" });
    
    response.EnsureSuccessStatusCode();
}
```

## Postman Testing

Add this header to your Postman requests:

```
X-Api-Mock-Scenario-ID: Coverages-Complete-i
```

Verify mock is active by checking response header:
```
X-Api-Mock-Response: true
```

## Extending Scenarios

To add new scenarios:

1. Add constant to `MockAvailityScenarios.cs`:
```csharp
public const string CoveragesCustom = "Coverages-Custom-i";
```

2. Add case to `InitiateCoverageCheckAsync` switch in `MockAvailityEligibilityClient.cs`:
```csharp
MockAvailityScenarios.CoveragesCustom => CreateCustomInitiateResponse(coverageId),
```

3. Add case to `PollCoverageStatusAsync` switch:
```csharp
MockAvailityScenarios.CoveragesCustom => CreateCustomPollResponse(),
```

4. Add response builder method:
```csharp
private static AvailityPollResponse CreateCustomPollResponse() =>
    new()
    {
        StatusCode = "4",
        Status = "Complete",
        Result = new AvailityEligibilityResult
        {
            PlanName = "Custom Plan",
            // ...
        }
    };
```

## Comparison with Availity Demo

This mock aligns with [Availity's Demo Services](https://developer.availity.com/blog/2025/10/31/availity-api-guide#demo):

| Availity Demo | Our Mock |
|---------------|----------|
| `X-Api-Mock-Scenario-ID` header | Same header pattern |
| `X-Api-Mock-Response: true` response | Same response header |
| Canned responses | Matching status codes and structure |
| No PHI in demo data | Same - all mock data |

## Troubleshooting

### Mock not being used

1. Check `AvailityMock:UseMock` is `true` in configuration
2. Verify `UseMockScenarioMiddleware()` is called in pipeline
3. Check service registration order in DI

### Scenario not changing

1. Ensure header is spelled correctly: `X-Api-Mock-Scenario-ID`
2. Call `mockClient.Reset()` between test cases
3. Check that `MockAvailityEligibilityClient` is registered as scoped (not singleton)

### Wrong response data

1. Verify scenario ID matches exactly (case-sensitive)
2. Check poll count tracking for async scenarios
3. Review the specific response builder method
