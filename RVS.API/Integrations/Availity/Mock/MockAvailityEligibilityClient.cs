using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RVS.Domain.Integrations.Availity;
using RVS.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace RVS.API.Integrations.Availity.Mock;

/// <summary>
/// Mock implementation of IAvailityEligibilityClient for testing.
/// Supports configurable scenarios and stateful polling simulation.
/// 
/// Usage:
/// 1. Unit Tests: Inject directly with configured scenario
/// 2. Integration Tests: Register as scoped service in DI
/// 3. Development: Use MockAvailityOptions.UseMock = true in appsettings
/// </summary>
public sealed class MockAvailityEligibilityClient : IAvailityEligibilityClient
{
    private readonly ILogger<MockAvailityEligibilityClient>? _logger;
    private readonly MockAvailityOptions _options;

    // Track poll counts per coverage ID for stateful scenarios
    private readonly Dictionary<string, int> _pollCounts = new();
    private readonly object _lock = new();

    public MockAvailityEligibilityClient(
        MockAvailityOptions? options = null,
        ILogger<MockAvailityEligibilityClient>? logger = null)
    {
        _options = options ?? new MockAvailityOptions();
        _logger = logger;
    }

    /// <summary>
    /// Set the scenario for subsequent calls.
    /// </summary>
    public string Scenario { get; set; } = MockAvailityScenarios.CoveragesPollingSuccess;  // Changed from CoveragesComplete

    /// <summary>
    /// Simulated network delay in milliseconds.
    /// </summary>
    public int SimulatedDelayMs { get; set; } = 50;

    /// <inheritdoc />
    public async Task<AvailityInitiateResponse> InitiateCoverageCheckAsync(
        AvailityEligibilityRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger?.LogInformation("[MOCK] Initiating coverage check for scenario: {Scenario}", Scenario);

        // Simulate network delay
        if (SimulatedDelayMs > 0)
            await Task.Delay(SimulatedDelayMs, cancellationToken);

        var coverageId = GenerateCoverageId();

        return Scenario switch
        {
            MockAvailityScenarios.CoveragesComplete => CreateCompleteInitiateResponse(coverageId),
            MockAvailityScenarios.CoveragesInProgress or
            MockAvailityScenarios.CoveragesPollingSuccess or
            MockAvailityScenarios.CoveragesPollingFailure or
            MockAvailityScenarios.CoveragesPollingTimeout => CreateInProgressInitiateResponse(coverageId),
            MockAvailityScenarios.CoveragesRetrying => CreateRetryingInitiateResponse(coverageId),
            MockAvailityScenarios.CoveragesPayerError1 => CreatePayerErrorResponse(coverageId, "Provider is ineligible for inquiries"),
            MockAvailityScenarios.CoveragesPayerError2 => CreatePayerErrorResponse(coverageId, "Subscriber name is invalid"),
            MockAvailityScenarios.CoveragesRequestError1 or
            MockAvailityScenarios.CoveragesRequestError2 => CreateRequestErrorResponse(request),
            MockAvailityScenarios.CoveragesCommunicationError => CreateCommunicationErrorResponse(coverageId),
            MockAvailityScenarios.CoveragesVision => CreateCompleteInitiateResponseWithVision(coverageId),
            MockAvailityScenarios.CoveragesMedicalHDHP => CreateCompleteInitiateResponseWithMedicalHdhp(coverageId),
            MockAvailityScenarios.CoveragesDental => CreateCompleteInitiateResponseWithDental(coverageId),
            _ => CreateInProgressInitiateResponse(coverageId)
        };
    }

    /// <inheritdoc />
    public async Task<AvailityPollResponse> PollCoverageStatusAsync(
        string availityCoverageId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(availityCoverageId);

        // Track poll count for this coverage ID
        int pollCount;
        lock (_lock)
        {
            _pollCounts.TryGetValue(availityCoverageId, out pollCount);
            _pollCounts[availityCoverageId] = ++pollCount;
        }

        _logger?.LogInformation("[MOCK] Polling coverage {CoverageId}, attempt #{PollCount}, scenario: {Scenario}",
            availityCoverageId, pollCount, Scenario);

        // Simulate network delay
        if (SimulatedDelayMs > 0)
            await Task.Delay(SimulatedDelayMs, cancellationToken);

        var response = Scenario switch
        {
            MockAvailityScenarios.CoveragesComplete => CreateCompletePollResponse(),
            MockAvailityScenarios.CoveragesPollingSuccess => CreatePollingSuccessResponse(pollCount),
            MockAvailityScenarios.CoveragesPollingFailure => CreatePollingFailureResponse(pollCount),
            MockAvailityScenarios.CoveragesPollingTimeout => CreateInProgressPollResponse(),
            MockAvailityScenarios.CoveragesInProgress => CreateInProgressPollResponse(),
            MockAvailityScenarios.CoveragesRetrying => pollCount >= 2 
                ? CreateCompletePollResponse() 
                : CreateRetryingPollResponse(),
            MockAvailityScenarios.CoveragesPayerError1 => CreatePayerErrorPollResponse("Provider is ineligible for inquiries"),
            MockAvailityScenarios.CoveragesPayerError2 => CreatePayerErrorPollResponse("Subscriber name is invalid"),
            MockAvailityScenarios.CoveragesCommunicationError => CreateCommunicationErrorPollResponse(),
            MockAvailityScenarios.CoveragesPartialResponse => CreatePartialPollResponse(),
            MockAvailityScenarios.CoveragesVision => CreateVisionPollResponse(),
            MockAvailityScenarios.CoveragesMedicalHDHP => CreateMedicalHdhpPollResponse(),
            MockAvailityScenarios.CoveragesDental => CreateDentalPollResponse(),
            _ => CreateCompletePollResponse()
        };

        _logger?.LogInformation("[MOCK] Returning StatusCode={StatusCode}, Status={Status}, HasResult={HasResult}",
            response.StatusCode, response.Status, response.Result != null);

        return response;
    }

    /// <summary>
    /// Reset poll counts (useful between test cases).
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _pollCounts.Clear();
        }
        Scenario = MockAvailityScenarios.CoveragesComplete;
    }

    // =====================================================
    // Response Builders
    // =====================================================

    private static string GenerateCoverageId() =>
        $"MOCK-{Guid.NewGuid():N}"[..32];

    private static AvailityInitiateResponse CreateCompleteInitiateResponse(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "4",
            Status = "Complete",
            EtaDate = null,
            Result = CreateMockEligibilityResult()
        };
    
    private static AvailityInitiateResponse CreateCompleteInitiateResponseWithVision(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "4",
            Status = "Complete",
            EtaDate = null,
            Result = CreateVisionEligibilityResult()
        };
    
    private static AvailityInitiateResponse CreateCompleteInitiateResponseWithMedicalHdhp(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "4",
            Status = "Complete",
            EtaDate = null,
            Result = CreateMedicalHdhpEligibilityResult()
        };
    
    private static AvailityInitiateResponse CreateCompleteInitiateResponseWithDental(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "4",
            Status = "Complete",
            EtaDate = null,
            Result = CreateDentalEligibilityResult()
        };

    private static AvailityInitiateResponse CreateInProgressInitiateResponse(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "0",
            Status = "In Progress",
            EtaDate = DateTime.UtcNow.AddSeconds(2)
        };

    private static AvailityInitiateResponse CreateRetryingInitiateResponse(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "R1",
            Status = "Retrying",
            EtaDate = DateTime.UtcNow.AddSeconds(3)
        };

    private static AvailityInitiateResponse CreatePayerErrorResponse(string coverageId, string message) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "19",
            Status = "Request Error",
            ErrorMessage = message,
            ValidationMessages =
            [
                new AvailityValidationMessage { Code = "AAA", ErrorMessage = message }
            ]
        };

    private static AvailityInitiateResponse CreateRequestErrorResponse(AvailityEligibilityRequest request)
    {
        var errors = new List<AvailityValidationMessage>();

        if (string.IsNullOrWhiteSpace(request.MemberId))
            errors.Add(new AvailityValidationMessage { Field = "memberId", ErrorMessage = "Member ID is required" });
        if (string.IsNullOrWhiteSpace(request.PayerId))
            errors.Add(new AvailityValidationMessage { Field = "payerId", ErrorMessage = "Payer ID is required" });

        if (errors.Count == 0)
            errors.Add(new AvailityValidationMessage { Field = "serviceType", ErrorMessage = "This field is required" });

        return new AvailityInitiateResponse
        {
            CoverageId = "",
            StatusCode = "400",
            Status = "Request Error",
            ErrorMessage = "Validation failed",
            ValidationMessages = errors
        };
    }

    private static AvailityInitiateResponse CreateCommunicationErrorResponse(string coverageId) =>
        new()
        {
            CoverageId = coverageId,
            StatusCode = "7",
            Status = "Communication Error",
            ErrorMessage = "The health plan did not respond"
        };

    private static AvailityPollResponse CreateCompletePollResponse() =>
        new()
        {
            StatusCode = "4",
            Status = "Complete",
            Result = CreateMockEligibilityResult()
        };

    private static AvailityPollResponse CreateInProgressPollResponse() =>
        new()
        {
            StatusCode = "0",
            Status = "In Progress",
            EtaDate = DateTime.UtcNow.AddSeconds(2)
        };

    private static AvailityPollResponse CreateRetryingPollResponse() =>
        new()
        {
            StatusCode = "R1",
            Status = "Retrying",
            EtaDate = DateTime.UtcNow.AddSeconds(3)
        };

    private static AvailityPollResponse CreatePollingSuccessResponse(int pollCount) =>
        pollCount >= 3 ? CreateCompletePollResponse() : CreateInProgressPollResponse();

    private static AvailityPollResponse CreatePollingFailureResponse(int pollCount) =>
        pollCount >= 4 
            ? CreatePayerErrorPollResponse("Coverage information unavailable") 
            : CreateInProgressPollResponse();

    private static AvailityPollResponse CreatePayerErrorPollResponse(string message) =>
        new()
        {
            StatusCode = "19",
            Status = "Request Error",
            ErrorMessage = message,
            ValidationMessages =
            [
                new AvailityValidationMessage { Code = "AAA", ErrorMessage = message }
            ]
        };

    private static AvailityPollResponse CreateCommunicationErrorPollResponse() =>
        new()
        {
            StatusCode = "7",
            Status = "Communication Error",
            ErrorMessage = "The health plan did not respond"
        };

    private static AvailityPollResponse CreatePartialPollResponse() =>
        new()
        {
            StatusCode = "3",
            Status = "Complete (Invalid Response)",
            Result = new AvailityEligibilityResult
            {
                PlanName = "Unknown Plan",
                CoverageLines = [],
                PayerNotes = ["Partial response received from payer"]
            }
        };

    // =====================================================
    // Specialized Coverage Responses
    // =====================================================

    private static AvailityPollResponse CreateVisionPollResponse() =>
        new()
        {
            StatusCode = "4",
            Status = "Complete",
            Result = new AvailityEligibilityResult
            {
                PlanName = "VSP Vision Gold",
                GroupNumber = "VIS123456",
                InsuranceType = "Vision",
                EligibilityStartDate = DateTime.UtcNow.AddYears(-1),
                EligibilityEndDate = DateTime.UtcNow.AddYears(1),
                Subscriber = new AvailitySubscriberInfo
                {
                    MemberId = "VSP001234567",
                    FirstName = "JOHN",
                    LastName = "DOE",
                    BirthDate = new DateTime(1985, 5, 15)
                },
                CoverageLines =
                [
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "35",
                        ServiceTypeDescription = "Vision - Exam",
                        CoverageType = "Copay",
                        Network = "InNetwork",
                        Amount = "10.00",
                        TimePeriod = "12 Months"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "35",
                        ServiceTypeDescription = "Vision - Frames",
                        CoverageType = "Allowance",
                        Network = "InNetwork",
                        Amount = "200.00",
                        TimePeriod = "24 Months"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "35",
                        ServiceTypeDescription = "Vision - Lenses",
                        CoverageType = "Copay",
                        Network = "InNetwork",
                        Amount = "25.00",
                        TimePeriod = "12 Months"
                    }
                ]
            }
        };

    private static AvailityPollResponse CreateMedicalHdhpPollResponse() =>
        new()
        {
            StatusCode = "4",
            Status = "Complete",
            Result = new AvailityEligibilityResult
            {
                PlanName = "BCBS HDHP Bronze",
                GroupNumber = "MED987654",
                GroupName = "ACME Corporation",
                InsuranceType = "Medical",
                EligibilityStartDate = DateTime.UtcNow.AddMonths(-6),
                EligibilityEndDate = DateTime.UtcNow.AddMonths(6),
                Subscriber = new AvailitySubscriberInfo
                {
                    MemberId = "BCB001234567",
                    FirstName = "JANE",
                    LastName = "SMITH",
                    BirthDate = new DateTime(1990, 8, 22)
                },
                CoverageLines =
                [
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "30",
                        ServiceTypeDescription = "Health Benefit Plan Coverage",
                        CoverageType = "Deductible",
                        Network = "InNetwork",
                        Amount = "3000.00",
                        Level = "Individual",
                        TimePeriod = "Calendar Year"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "30",
                        ServiceTypeDescription = "Health Benefit Plan Coverage",
                        CoverageType = "Deductible",
                        Network = "InNetwork",
                        Amount = "6000.00",
                        Level = "Family",
                        TimePeriod = "Calendar Year"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "30",
                        ServiceTypeDescription = "Health Benefit Plan Coverage",
                        CoverageType = "Coinsurance",
                        Network = "InNetwork",
                        Amount = "20%",
                        Notes = "After deductible"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "98",
                        ServiceTypeDescription = "Professional (Physician) Visit - Office",
                        CoverageType = "Coinsurance",
                        Network = "InNetwork",
                        Amount = "20%",
                        AuthorizationRequired = false
                    }
                ]
            }
        };

    private static AvailityPollResponse CreateDentalPollResponse() =>
        new()
        {
            StatusCode = "4",
            Status = "Complete",
            Result = new AvailityEligibilityResult
            {
                PlanName = "Delta Dental PPO",
                GroupNumber = "DEN456789",
                InsuranceType = "Dental",
                EligibilityStartDate = DateTime.UtcNow.AddYears(-2),
                Subscriber = new AvailitySubscriberInfo
                {
                    MemberId = "DLT001234567",
                    FirstName = "ROBERT",
                    LastName = "JOHNSON"
                },
                CoverageLines =
                [
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "23",
                        ServiceTypeDescription = "Dental - Preventive",
                        CoverageType = "Coinsurance",
                        Network = "InNetwork",
                        Amount = "0%",
                        Notes = "Covered at 100%"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "24",
                        ServiceTypeDescription = "Dental - Basic",
                        CoverageType = "Coinsurance",
                        Network = "InNetwork",
                        Amount = "20%"
                    },
                    new AvailityCoverageLine
                    {
                        ServiceTypeCode = "25",
                        ServiceTypeDescription = "Dental - Major",
                        CoverageType = "Coinsurance",
                        Network = "InNetwork",
                        Amount = "50%"
                    }
                ]
            }
        };

    private static AvailityEligibilityResult CreateMockEligibilityResult() =>
        new()
        {
            PlanName = "Mock Insurance Gold PPO",
            GroupNumber = "GRP123456",
            GroupName = "Mock Employer Inc.",
            InsuranceType = "Medical",
            EligibilityStartDate = DateTime.UtcNow.AddYears(-1),
            EligibilityEndDate = DateTime.UtcNow.AddYears(1),
            CoverageStartDate = DateTime.UtcNow.AddYears(-1),
            CoverageEndDate = DateTime.UtcNow.AddYears(1),
            Subscriber = new AvailitySubscriberInfo
            {
                MemberId = "MOCK123456789",
                FirstName = "MOCK",
                LastName = "PATIENT",
                BirthDate = new DateTime(1980, 1, 15),
                Gender = "M"
            },
            CoverageLines =
            [
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Active",
                    Network = "InNetwork"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Copay",
                    Network = "InNetwork",
                    Amount = "25.00",
                    TimePeriod = "Visit"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Deductible",
                    Network = "InNetwork",
                    Amount = "500.00",
                    Level = "Individual",
                    TimePeriod = "Calendar Year"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Coinsurance",
                    Network = "InNetwork",
                    Amount = "20%"
                }
            ],
            PayerNotes =
            [
                "This is a mock response for testing purposes.",
                "Contact member services at 1-800-MOCK-INS for questions."
            ]
        };

    private static AvailityEligibilityResult CreateVisionEligibilityResult() =>
        new()
        {
            PlanName = "VSP Vision Gold",
            GroupNumber = "VIS123456",
            InsuranceType = "Vision",
            EligibilityStartDate = DateTime.UtcNow.AddYears(-1),
            EligibilityEndDate = DateTime.UtcNow.AddYears(1),
            Subscriber = new AvailitySubscriberInfo
            {
                MemberId = "VSP001234567",
                FirstName = "JOHN",
                LastName = "DOE",
                BirthDate = new DateTime(1985, 5, 15)
            },
            CoverageLines =
            [
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "35",
                    ServiceTypeDescription = "Vision - Exam",
                    CoverageType = "Copay",
                    Network = "InNetwork",
                    Amount = "10.00",
                    TimePeriod = "12 Months"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "35",
                    ServiceTypeDescription = "Vision - Frames",
                    CoverageType = "Allowance",
                    Network = "InNetwork",
                    Amount = "200.00",
                    TimePeriod = "24 Months"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "35",
                    ServiceTypeDescription = "Vision - Lenses",
                    CoverageType = "Copay",
                    Network = "InNetwork",
                    Amount = "25.00",
                    TimePeriod = "12 Months"
                }
            ]
        };

    private static AvailityEligibilityResult CreateMedicalHdhpEligibilityResult() =>
        new()
        {
            PlanName = "BCBS HDHP Bronze",
            GroupNumber = "MED987654",
            GroupName = "ACME Corporation",
            InsuranceType = "Medical",
            EligibilityStartDate = DateTime.UtcNow.AddMonths(-6),
            EligibilityEndDate = DateTime.UtcNow.AddMonths(6),
            Subscriber = new AvailitySubscriberInfo
            {
                MemberId = "BCB001234567",
                FirstName = "JANE",
                LastName = "SMITH",
                BirthDate = new DateTime(1990, 8, 22)
            },
            CoverageLines =
            [
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Deductible",
                    Network = "InNetwork",
                    Amount = "3000.00",
                    Level = "Individual",
                    TimePeriod = "Calendar Year"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Deductible",
                    Network = "InNetwork",
                    Amount = "6000.00",
                    Level = "Family",
                    TimePeriod = "Calendar Year"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "30",
                    ServiceTypeDescription = "Health Benefit Plan Coverage",
                    CoverageType = "Coinsurance",
                    Network = "InNetwork",
                    Amount = "20%",
                    Notes = "After deductible"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "98",
                    ServiceTypeDescription = "Professional (Physician) Visit - Office",
                    CoverageType = "Coinsurance",
                    Network = "InNetwork",
                    Amount = "20%",
                    AuthorizationRequired = false
                }
            ]
        };

    private static AvailityEligibilityResult CreateDentalEligibilityResult() =>
        new()
        {
            PlanName = "Delta Dental PPO",
            GroupNumber = "DEN456789",
            InsuranceType = "Dental",
            EligibilityStartDate = DateTime.UtcNow.AddYears(-2),
            Subscriber = new AvailitySubscriberInfo
            {
                MemberId = "DLT001234567",
                FirstName = "ROBERT",
                LastName = "JOHNSON"
            },
            CoverageLines =
            [
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "23",
                    ServiceTypeDescription = "Dental - Preventive",
                    CoverageType = "Coinsurance",
                    Network = "InNetwork",
                    Amount = "0%",
                    Notes = "Covered at 100%"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "24",
                    ServiceTypeDescription = "Dental - Basic",
                    CoverageType = "Coinsurance",
                    Network = "InNetwork",
                    Amount = "20%"
                },
                new AvailityCoverageLine
                {
                    ServiceTypeCode = "25",
                    ServiceTypeDescription = "Dental - Major",
                    CoverageType = "Coinsurance",
                    Network = "InNetwork",
                    Amount = "50%"
                }
            ]
        };
}

/// <summary>
/// Configuration options for mock Availity client.
/// </summary>
public sealed class MockAvailityOptions
{
    /// <summary>
    /// When true, use mock client instead of real Availity API.
    /// </summary>
    public bool UseMock { get; set; }

    /// <summary>
    /// Default scenario to use when not specified per-request.
    /// </summary>
    public string DefaultScenario { get; set; } = MockAvailityScenarios.CoveragesComplete;

    /// <summary>
    /// Simulated network delay in milliseconds.
    /// </summary>
    public int SimulatedDelayMs { get; set; } = 50;
}
