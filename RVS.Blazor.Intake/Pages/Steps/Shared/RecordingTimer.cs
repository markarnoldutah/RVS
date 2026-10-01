namespace RVS.Blazor.Intake.Pages.Steps.Shared;

/// <summary>
/// Ticks once per second while a voice recording is active and triggers an auto-stop
/// callback at <paramref name="maxSeconds"/> so a forgotten recording can't run forever.
/// Shared by every wizard step with mic capture (Step 3 VIN lookup, Step 5 issue description).
/// </summary>
public sealed class RecordingTimer(int maxSeconds, Action onTick, Func<Task> onAutoStop) : IDisposable
{
    private CancellationTokenSource? _cts;

    public int ElapsedSeconds { get; private set; }

    public void Start()
    {
        Stop();
        ElapsedSeconds = 0;
        _cts = new CancellationTokenSource();
        _ = RunAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                ElapsedSeconds++;
                onTick();

                if (ElapsedSeconds >= maxSeconds)
                {
                    await onAutoStop();
                    break;
                }
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Dispose() => Stop();
}
