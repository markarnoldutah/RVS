namespace RVS.API.Integrations.Availity.Mock;

/// <summary>
/// Defines mock scenario IDs for Availity Coverages API testing.
/// These align with Availity's demo X-Api-Mock-Scenario-ID pattern.
/// 
/// Usage:
/// - In tests: Configure MockAvailityEligibilityClient with desired scenario
/// - In HTTP: Send X-Api-Mock-Scenario-ID header with scenario ID
/// </summary>
public static class MockAvailityScenarios
{
    // =====================================================
    // Coverages API Scenarios (Eligibility/Benefits)
    // =====================================================

    /// <summary>
    /// Coverage check completes immediately with full benefits data.
    /// Status: Complete (statusCode "4")
    /// </summary>
    public const string CoveragesComplete = "Coverages-Complete-i";

    /// <summary>
    /// Coverage check is in progress (async polling required).
    /// Status: In Progress (statusCode "0")
    /// </summary>
    public const string CoveragesInProgress = "Coverages-InProgress-i";

    /// <summary>
    /// Health plan is retrying the request.
    /// Status: Retrying (statusCode "R1")
    /// </summary>
    public const string CoveragesRetrying = "Coverages-Retrying-i";

    /// <summary>
    /// Payer returned an error - provider ineligible for inquiries.
    /// Status: Payer Error (statusCode "19")
    /// </summary>
    public const string CoveragesPayerError1 = "Coverages-PayerError1-i";

    /// <summary>
    /// Payer returned an error - subscriber name invalid.
    /// Status: Payer Error (statusCode "19")
    /// </summary>
    public const string CoveragesPayerError2 = "Coverages-PayerError2-i";

    /// <summary>
    /// Request validation failed at Availity.
    /// Status: Request Error (statusCode "400")
    /// </summary>
    public const string CoveragesRequestError1 = "Coverages-RequestError1-i";

    /// <summary>
    /// Request validation failed - missing required fields.
    /// Status: Request Error (statusCode "400")
    /// </summary>
    public const string CoveragesRequestError2 = "Coverages-RequestError2-i";

    /// <summary>
    /// Communication error - payer timeout.
    /// Status: Communication Error (statusCode "7")
    /// </summary>
    public const string CoveragesCommunicationError = "Coverages-CommunicationError-i";

    /// <summary>
    /// Coverage completes with partial/invalid response from payer.
    /// Status: Complete (Invalid Response) (statusCode "3")
    /// </summary>
    public const string CoveragesPartialResponse = "Coverages-PartialResponse-i";

    // =====================================================
    // Polling Simulation Scenarios
    // =====================================================

    /// <summary>
    /// Simulates async flow: InProgress on first 2 polls, then Complete.
    /// Use this to test the full polling lifecycle.
    /// </summary>
    public const string CoveragesPollingSuccess = "Coverages-Polling-Success-i";

    /// <summary>
    /// Simulates async flow: InProgress on first 3 polls, then Payer Error.
    /// Use this to test error handling after polling.
    /// </summary>
    public const string CoveragesPollingFailure = "Coverages-Polling-Failure-i";

    /// <summary>
    /// Simulates slow payer: InProgress for 10+ polls.
    /// Use this to test max poll attempts logic.
    /// </summary>
    public const string CoveragesPollingTimeout = "Coverages-Polling-Timeout-i";

    // =====================================================
    // Specific Coverage Types
    // =====================================================

    /// <summary>
    /// Vision-specific coverage with frames/lenses benefits.
    /// </summary>
    public const string CoveragesVision = "Coverages-Vision-i";

    /// <summary>
    /// Medical coverage with high deductible plan details.
    /// </summary>
    public const string CoveragesMedicalHDHP = "Coverages-Medical-HDHP-i";

    /// <summary>
    /// Dental coverage with orthodontia benefits.
    /// </summary>
    public const string CoveragesDental = "Coverages-Dental-i";

    /// <summary>
    /// Medicare Advantage plan coverage.
    /// </summary>
    public const string CoveragesMedicare = "Coverages-Medicare-i";

    /// <summary>
    /// Medicaid coverage with state-specific details.
    /// </summary>
    public const string CoveragesMedicaid = "Coverages-Medicaid-i";

    // =====================================================
    // COB (Coordination of Benefits) Scenarios
    // =====================================================

    /// <summary>
    /// Primary coverage - patient is subscriber.
    /// </summary>
    public const string CoveragesPrimary = "Coverages-COB-Primary-i";

    /// <summary>
    /// Secondary coverage with primary payer information.
    /// </summary>
    public const string CoveragesSecondary = "Coverages-COB-Secondary-i";
}
