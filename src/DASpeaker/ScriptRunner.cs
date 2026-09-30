namespace DASpeaker;

internal enum PlaybackState { Idle, Running, Pausing, Paused, Stopping, Completed }

internal sealed class ScriptRunner(IChatInput input, TimeProvider clock)
{
    public event Action? Changed;
    public event Action<string>? Trace;
    public PlaybackState State { get; private set; }
    public bool IsActive { get; private set; }
    public int StepIndex { get; private set; }
    public int SentCount { get; private set; }
    public int TotalMessages { get; private set; }
    private bool pauseRequested;
    private bool stopRequested;
    private bool sending;
    private bool waiting;
    private TimeSpan remaining;
    private long waitStarted;
    private CancellationTokenSource? wake;
    private TaskCompletionSource? resumed;
    public TimeSpan RemainingDelay => waiting ? MaxZero(remaining - clock.GetElapsedTime(waitStarted)) : remaining;
    public string? LastError { get; private set; }
    public bool RecoveryRequired => LastError is not null;
    public async Task StartAsync(ScriptPlan plan, ClientTarget target, InputTimings timings)
    {
        if (IsActive) throw new InvalidOperationException("Playback is already active.");
        if (!plan.IsValid) throw new InvalidOperationException("Fix script validation errors before starting.");
        var steps = plan.Steps.ToArray();
        IsActive = true;
        pauseRequested = stopRequested = false;
        LastError = null;
        StepIndex = SentCount = 0;
        TotalMessages = plan.MessageCount;
        remaining = TimeSpan.Zero;
        SetState(PlaybackState.Running);
        try
        {
            while (StepIndex < steps.Length && !stopRequested)
            {
                await WaitForResumeAsync();
                if (stopRequested) break;
                var step = steps[StepIndex];
                if (step.IsMessage)
                {
                    sending = true;
                    Changed?.Invoke();
                    try
                    {
                        Trace?.Invoke($"MESSAGE {SentCount + 1}/{TotalMessages} | source line {step.SourceLine} | {step.Text!.Length} chars | {step.Text}");
                        // Stop and pause take effect between messages. Preserve input cleanup.
                        await input.SendAsync(target, step.Text!, timings, CancellationToken.None);
                        SentCount++;
                        Trace?.Invoke($"MESSAGE {SentCount}/{TotalMessages} dispatched; game receipt unconfirmed.");
                        StepIndex++;
                    }
                    catch (Exception ex)
                    {
                        LastError = ex.Message;
                        pauseRequested = true;
                    }
                    finally { sending = false; }
                }
                else
                {
                    remaining = TimeSpan.FromMilliseconds(step.DelayMs);
                    Trace?.Invoke($"WAIT {step.DelayMs} ms ({step.Reason}; source line {step.SourceLine}).");
                    while (remaining > TimeSpan.Zero && !stopRequested)
                    {
                        await WaitForResumeAsync();
                        if (stopRequested) break;
                        using var wait = new CancellationTokenSource();
                        wake = wait;
                        waitStarted = clock.GetTimestamp();
                        waiting = true;
                        var delay = Task.Delay(remaining, clock, wait.Token);
                        Changed?.Invoke();
                        try { await delay; }
                        catch (OperationCanceledException) when (wait.IsCancellationRequested) { }
                        finally
                        {
                            remaining = RemainingDelay;
                            waiting = false;
                            wake = null;
                        }
                    }
                    if (!stopRequested) StepIndex++;
                }
                Changed?.Invoke();
            }
            if (stopRequested)
            {
                StepIndex = SentCount = 0;
                remaining = TimeSpan.Zero;
                State = PlaybackState.Idle;
            }
            else State = PlaybackState.Completed;
        }
        finally
        {
            IsActive = sending = waiting = false;
            resumed = null;
            wake = null;
            Changed?.Invoke();
        }
    }

    public void Pause()
    {
        if (!IsActive || stopRequested || pauseRequested) return;
        pauseRequested = true;
        // Capture elapsed time now; scheduling the cancellation continuation must not consume paused time.
        if (waiting)
        {
            remaining = RemainingDelay;
            waiting = false;
        }
        wake?.Cancel();
        SetState(sending ? PlaybackState.Pausing : PlaybackState.Paused);
    }

    public void Resume(bool acknowledgeRecovery = false)
    {
        if (!IsActive || stopRequested || !pauseRequested) return;
        if (RecoveryRequired && !acknowledgeRecovery)
            throw new InvalidOperationException("Check the client and clear pending chat before retrying this message.");
        LastError = null;
        pauseRequested = false;
        SetState(PlaybackState.Running);
        resumed?.TrySetResult();
    }

    public void Stop()
    {
        if (!IsActive) { StepIndex = SentCount = 0; remaining = TimeSpan.Zero; SetState(PlaybackState.Idle); return; }
        stopRequested = true;
        SetState(PlaybackState.Stopping);
        wake?.Cancel();
        resumed?.TrySetResult();
    }

    private async Task WaitForResumeAsync()
    {
        if (!pauseRequested || stopRequested) return;
        resumed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        SetState(PlaybackState.Paused);
        await resumed.Task;
        resumed = null;
    }

    private void SetState(PlaybackState state) { State = state; Changed?.Invoke(); }
    private static TimeSpan MaxZero(TimeSpan value) => value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
