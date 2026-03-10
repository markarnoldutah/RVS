using RVS.Domain.DTOs;
using Microsoft.Extensions.Logging;
using Timer = System.Timers.Timer;

namespace RVS.BlazorWASM.Services;

/// <summary>
/// Polling service for eligibility checks with RU-optimized strategy.
/// </summary>
public interface IEligibilityCheckPollingService : IDisposable
{
    /// <summary>
    /// Starts polling for eligibility checks that are in progress.
    /// </summary>
    void StartPolling(
        string practiceId,
        string patientId,
        string encounterId,
        Action<List<EligibilityCheckSummaryResponseDto>> onUpdate,
        Action<Exception>? onError = null);

    /// <summary>
    /// Stops the polling timer.
    /// </summary>
    void StopPolling();

    /// <summary>
    /// Manually triggers a poll cycle (useful for initial load).
    /// </summary>
    Task PollNowAsync();

    /// <summary>
    /// Indicates whether polling is currently active.
    /// </summary>
    bool IsPolling { get; }
}

public class EligibilityCheckPollingService : IEligibilityCheckPollingService
{
    private readonly IEligibilityCheckApiService _apiService;
    private readonly ILogger<EligibilityCheckPollingService> _logger;

    private Timer? _pollTimer;
    private string? _practiceId;
    private string? _patientId;
    private string? _encounterId;
    private Action<List<EligibilityCheckSummaryResponseDto>>? _onUpdate;
    private Action<Exception>? _onError;
    private CancellationTokenSource? _cts;
    private bool _isPolling;

    private const int PollIntervalMs = 2000;
    private const int MaxConsecutiveErrors = 3;

    private int _consecutiveErrors;

    public EligibilityCheckPollingService(
        IEligibilityCheckApiService apiService,
        ILogger<EligibilityCheckPollingService> logger)
    {
        _apiService = apiService;
        _logger = logger;
    }

    public bool IsPolling => _isPolling;

    public void StartPolling(
        string practiceId,
        string patientId,
        string encounterId,
        Action<List<EligibilityCheckSummaryResponseDto>> onUpdate,
        Action<Exception>? onError = null)
    {
        StopPolling();

        _practiceId = practiceId;
        _patientId = patientId;
        _encounterId = encounterId;
        _onUpdate = onUpdate;
        _onError = onError;
        _cts = new CancellationTokenSource();
        _consecutiveErrors = 0;
        _isPolling = true;

        _pollTimer = new Timer(PollIntervalMs);
        _pollTimer.Elapsed += async (sender, e) => await PollChecksAsync();
        _pollTimer.AutoReset = true;
        _pollTimer.Start();

        _logger.LogInformation(
            "Started polling for encounter {EncounterId} (interval: {Interval}ms)",
            encounterId,
            PollIntervalMs);

        _ = PollChecksAsync();
    }

    public void StopPolling()
    {
        _pollTimer?.Stop();
        _pollTimer?.Dispose();
        _pollTimer = null;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
        _isPolling = false;

        _logger.LogInformation("Stopped polling");
    }

    public async Task PollNowAsync()
    {
        await PollChecksAsync();
    }

    private async Task PollChecksAsync()
    {
        if (_practiceId == null || _patientId == null || _encounterId == null || _onUpdate == null)
            return;

        if (_cts?.Token.IsCancellationRequested == true)
            return;

        try
        {
            // Step 1: Get current state
            var checks = await _apiService.GetEligibilityChecksAsync(
                _practiceId,
                _patientId,
                _encounterId,
                _cts?.Token ?? CancellationToken.None);

            // Step 2: Determine which checks need polling based on NextPollAfterUtc
            var now = DateTime.UtcNow;
            var checksReadyToPoll = checks
                .Where(c => c.Status == "InProgress" || c.Status == "Pending")
                .Where(c => c.NextPollAfterUtc == null || now >= c.NextPollAfterUtc)
                .Select(c => c.EligibilityCheckId)
                .ToList();

            _logger.LogDebug(
                "Poll cycle: {TotalChecks} total, {InProgressCount} in-progress, {ReadyToPollCount} ready to poll",
                checks.Count,
                checks.Count(c => c.Status == "InProgress" || c.Status == "Pending"),
                checksReadyToPoll.Count);

            // Step 3: If checks are ready, poll them using selective API
            if (checksReadyToPoll.Count > 0)
            {
                checks = await _apiService.GetEligibilityChecksWithPollingAsync(
                    _practiceId,
                    _patientId,
                    _encounterId,
                    checksReadyToPoll,
                    _cts?.Token ?? CancellationToken.None);

                _logger.LogInformation(
                    "Polled {Count} checks via selective API",
                    checksReadyToPoll.Count);
            }

            // Step 4: Update UI
            _onUpdate(checks);

            // Reset error counter on success
            _consecutiveErrors = 0;

            // Step 5: Stop polling if all checks are terminal
            var hasActiveChecks = checks.Any(c => c.Status == "InProgress" || c.Status == "Pending");
            if (!hasActiveChecks)
            {
                _logger.LogInformation(
                    "All checks reached terminal state, stopping polling");
                StopPolling();
            }
        }
        catch (Exception ex)
        {
            _consecutiveErrors++;
            _logger.LogError(
                ex,
                "Polling error ({ErrorCount}/{MaxErrors})",
                _consecutiveErrors,
                MaxConsecutiveErrors);

            _onError?.Invoke(ex);

            if (_consecutiveErrors >= MaxConsecutiveErrors)
            {
                _logger.LogError(
                    "Exceeded maximum consecutive errors, stopping polling");
                StopPolling();
            }
        }
    }

    public void Dispose()
    {
        StopPolling();
    }
}
