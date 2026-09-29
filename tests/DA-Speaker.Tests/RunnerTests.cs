using DASpeaker;
using Xunit;

namespace DASpeaker.Tests;

public sealed class RunnerTests
{
    private static readonly ClientTarget Target = new((nint)123, 456, "Antony");
    private static ScriptPlan Plan() => ScriptParser.Parse("First\nSecond", new(false, 3500, 5000));

    [Fact]
    public async Task CeremonyEndingIncludesPenultimatePackedMessage()
    {
        const string script = "Lady Glioca, let your mercy remain with us tonight.\nLet us carry enough beyond these walls for ourselves,\nand a little extra for whoever decides to test it first.\nSomeone will. We have all lived in Temuair long enough.\nGo beneath Glioca's light, Aislings, and go in peace.";
        var input = new RecordingInput();
        var runner = new ScriptRunner(input, new ManualClock());
        await runner.StartAsync(ScriptParser.Parse(script, new(true, 0, 0)), Target, new(InputMethod: InputMethod.CtrlVDirect));
        Assert.Equal(5, input.Messages.Count);
        Assert.Equal("We have all lived in Temuair long enough. Go beneath", input.Messages[^2]);
        Assert.Equal("Glioca's light, Aislings, and go in peace.", input.Messages[^1]);
        Assert.Equal(5, runner.SentCount);
    }

    [Fact]
    public async Task SendsMessagesInOrderAndCompletesWithoutFinalDelay()
    {
        var clock = new ManualClock();
        var input = new RecordingInput();
        var runner = new ScriptRunner(input, clock);
        var run = runner.StartAsync(Plan(), Target, new(InputMethod: InputMethod.CtrlVDirect));
        await Until(() => runner.RemainingDelay > TimeSpan.Zero);
        Assert.Equal(new[] { "First" }, input.Messages);
        clock.Advance(3499);
        Assert.Single(input.Messages);
        clock.Advance(1);
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "First", "Second" }, input.Messages);
        Assert.Equal(PlaybackState.Completed, runner.State);
        Assert.Equal(2, runner.SentCount);
    }

    [Fact]
    public async Task PauseFreezesRemainingDelayAndResumeKeepsPosition()
    {
        var clock = new ManualClock();
        var input = new RecordingInput();
        var runner = new ScriptRunner(input, clock);
        var run = runner.StartAsync(Plan(), Target, new());
        await Until(() => runner.RemainingDelay > TimeSpan.Zero);
        clock.Advance(1000);
        runner.Pause();
        await Until(() => runner.State == PlaybackState.Paused);
        Assert.Equal(TimeSpan.FromMilliseconds(2500), runner.RemainingDelay);
        clock.Advance(20000);
        Assert.Single(input.Messages);
        runner.Resume();
        await Until(() => runner.State == PlaybackState.Running && clock.HasTimer);
        clock.Advance(2499);
        Assert.Single(input.Messages);
        clock.Advance(1);
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(new[] { "First", "Second" }, input.Messages);
    }

    [Fact]
    public async Task PauseDuringMessageFinishesMessageThenPauses()
    {
        var clock = new ManualClock();
        var input = new RecordingInput { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var runner = new ScriptRunner(input, clock);
        var run = runner.StartAsync(Plan(), Target, new());
        runner.Pause();
        Assert.Equal(PlaybackState.Pausing, runner.State);
        input.Block.SetResult();
        await Until(() => runner.State == PlaybackState.Paused);
        Assert.Equal(1, runner.SentCount);
        runner.Stop();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(input.Messages);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopCancelsFutureMessagesAndResetsPosition(bool paused)
    {
        var clock = new ManualClock();
        var input = new RecordingInput();
        var runner = new ScriptRunner(input, clock);
        var run = runner.StartAsync(Plan(), Target, new());
        await Until(() => runner.RemainingDelay > TimeSpan.Zero);
        if (paused) { runner.Pause(); await Until(() => runner.State == PlaybackState.Paused); }
        runner.Stop();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        clock.Advance(100000);
        Assert.Single(input.Messages);
        Assert.Equal(0, runner.StepIndex);
        Assert.Equal(0, runner.SentCount);
        Assert.Equal(PlaybackState.Idle, runner.State);
    }

    [Fact]
    public async Task StopDuringSendWaitsForCurrentMessageCleanup()
    {
        var input = new RecordingInput { Block = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var runner = new ScriptRunner(input, new ManualClock());
        var run = runner.StartAsync(Plan(), Target, new());
        runner.Stop();
        Assert.False(run.IsCompleted);
        Assert.False(input.Token.IsCancellationRequested);
        input.Block.SetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Single(input.Messages);
        Assert.Equal(PlaybackState.Idle, runner.State);
    }

    [Fact]
    public async Task RejectsOverlappingStart()
    {
        var runner = new ScriptRunner(new RecordingInput(), new ManualClock());
        var run = runner.StartAsync(Plan(), Target, new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.StartAsync(Plan(), Target, new()));
        runner.Stop();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task FailurePausesAtSameMessageUntilExplicitRecovery()
    {
        var input = new RecordingInput { Failure = new InvalidOperationException("Dark Ages client is no longer available.") };
        var runner = new ScriptRunner(input, new ManualClock());
        var run = runner.StartAsync(ScriptParser.Parse("Hello", new()), Target, new());
        await Until(() => runner.State == PlaybackState.Paused);
        Assert.True(runner.RecoveryRequired);
        Assert.Equal(0, runner.StepIndex);
        Assert.Equal(0, runner.SentCount);
        Assert.Throws<InvalidOperationException>(() => runner.Resume());
        input.Failure = null;
        runner.Resume(acknowledgeRecovery: true);
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, runner.SentCount);
    }

    [Fact]
    public async Task StopPreservesRecoveryWarningFromFailedInput()
    {
        var input = new RecordingInput { Failure = new InvalidOperationException("Partial input failed") };
        var runner = new ScriptRunner(input, new ManualClock());
        var run = runner.StartAsync(Plan(), Target, new());
        await Until(() => runner.State == PlaybackState.Paused);
        runner.Stop();
        await run.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(runner.RecoveryRequired);
        Assert.Equal(0, runner.StepIndex);
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var i = 0; i < 3000; i++)
        {
            if (condition()) return;
            await Task.Delay(1);
        }
        Assert.Fail("Runner did not reach the expected state.");
    }

    private sealed class RecordingInput : IChatInput
    {
        public List<string> Messages { get; } = [];
        public TaskCompletionSource? Block;
        public Exception? Failure;
        public CancellationToken Token;
        public async Task SendAsync(ClientTarget target, string text, InputTimings timings, CancellationToken token)
        {
            Token = token;
            if (Failure is not null) throw Failure;
            Messages.Add(text);
            if (Block is not null) await Block.Task;
        }
    }

    private sealed class ManualClock : TimeProvider
    {
        private long ticks;
        private readonly List<ManualTimer> timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(ticks);
        public bool HasTimer { get { lock (timers) return timers.Any(t => !t.Disposed && t.Due != long.MaxValue); } }
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            lock (timers)
            {
                var timer = new ManualTimer(this, callback, state);
                timer.Change(dueTime, period);
                timers.Add(timer);
                return timer;
            }
        }
        public void Advance(int milliseconds)
        {
            List<ManualTimer> due;
            lock (timers)
            {
                ticks += TimeSpan.FromMilliseconds(milliseconds).Ticks;
                due = timers.Where(t => !t.Disposed && t.Due <= ticks).ToList();
                foreach (var timer in due) timer.Due = long.MaxValue;
            }
            foreach (var timer in due) timer.Fire();
        }
        private sealed class ManualTimer(ManualClock owner, TimerCallback callback, object? state) : ITimer
        {
            public bool Disposed;
            public long Due;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                lock (owner.timers)
                {
                    Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : owner.ticks + dueTime.Ticks;
                    return !Disposed;
                }
            }
            public void Fire() => callback(state);
            public void Dispose() { lock (owner.timers) Disposed = true; }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
