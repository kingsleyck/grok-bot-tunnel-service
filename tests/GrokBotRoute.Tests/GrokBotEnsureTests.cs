namespace GrokBotRoute.Tests;

public sealed class GrokBotEnsureTests
{
    private readonly GrokBotEnsure _ensure = new();

    [Fact]
    public async Task Running_app_does_not_start()
    {
        var starts = 0;
        var queries = 0;
        var sleeps = 0;

        var result = await _ensure.EnsureAsync(
            isRunning: true,
            executableExists: true,
            hasInteractiveSession: true,
            startAsync: _ =>
            {
                starts++;
                return Task.CompletedTask;
            },
            queryRunningAsync: _ =>
            {
                queries++;
                return Task.FromResult(true);
            },
            wait: TimeSpan.FromSeconds(10),
            getNow: () => DateTimeOffset.UnixEpoch,
            delayAsync: (_, _) =>
            {
                sleeps++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.AlreadyRunning, result.Action);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.Started);
        Assert.Equal(0, starts);
        Assert.Equal(0, queries);
        Assert.Equal(0, sleeps);
    }

    [Fact]
    public async Task Missing_executable_does_not_start()
    {
        var starts = 0;
        var result = await _ensure.EnsureAsync(
            isRunning: false,
            executableExists: false,
            hasInteractiveSession: true,
            startAsync: _ =>
            {
                starts++;
                return Task.CompletedTask;
            },
            queryRunningAsync: _ => Task.FromResult(true),
            wait: TimeSpan.FromSeconds(10),
            getNow: () => DateTimeOffset.UnixEpoch,
            delayAsync: (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.Missing, result.Action);
        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Started);
        Assert.Equal(0, starts);
    }

    [Fact]
    public void Live_process_stays_already_running_when_the_file_check_is_false()
    {
        var result = _ensure.Plan(isRunning: true, executableExists: false, hasInteractiveSession: false);
        Assert.Equal(GrokBotEnsureAction.AlreadyRunning, result.Action);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task No_interactive_session_waits_without_starting()
    {
        var starts = 0;
        var result = await _ensure.EnsureAsync(
            isRunning: false,
            executableExists: true,
            hasInteractiveSession: false,
            startAsync: _ =>
            {
                starts++;
                return Task.CompletedTask;
            },
            queryRunningAsync: _ => Task.FromResult(true),
            wait: TimeSpan.FromSeconds(10),
            getNow: () => DateTimeOffset.UnixEpoch,
            delayAsync: (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.WaitingForSession, result.Action);
        Assert.Equal(0, result.ExitCode);
        Assert.False(result.Started);
        Assert.Equal(0, starts);
    }

    [Fact]
    public async Task Start_exception_does_not_wait()
    {
        var queries = 0;
        var result = await _ensure.EnsureAsync(
            isRunning: false,
            executableExists: true,
            hasInteractiveSession: true,
            startAsync: _ => throw new InvalidOperationException("start failed"),
            queryRunningAsync: _ =>
            {
                queries++;
                return Task.FromResult(true);
            },
            wait: TimeSpan.FromSeconds(10),
            getNow: () => DateTimeOffset.UnixEpoch,
            delayAsync: (_, _) => Task.CompletedTask,
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.StartFailed, result.Action);
        Assert.Equal(1, result.ExitCode);
        Assert.False(result.Started);
        Assert.Equal(0, queries);
    }

    [Fact]
    public async Task Immediate_process_does_not_sleep()
    {
        var sleeps = 0;
        var result = await _ensure.EnsureAsync(
            isRunning: false,
            executableExists: true,
            hasInteractiveSession: true,
            startAsync: _ => Task.CompletedTask,
            queryRunningAsync: _ => Task.FromResult(true),
            wait: TimeSpan.FromSeconds(10),
            getNow: () => DateTimeOffset.UnixEpoch,
            delayAsync: (_, _) =>
            {
                sleeps++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.Started, result.Action);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Started);
        Assert.Equal(0, sleeps);
    }

    [Fact]
    public async Task Deadline_already_reached_does_not_sleep()
    {
        var sleeps = 0;
        var queries = 0;
        var clock = 0;
        var result = await _ensure.EnsureAsync(
            isRunning: false,
            executableExists: true,
            hasInteractiveSession: true,
            startAsync: _ => Task.CompletedTask,
            queryRunningAsync: _ =>
            {
                queries++;
                return Task.FromResult(false);
            },
            wait: TimeSpan.FromSeconds(10),
            getNow: () =>
            {
                clock++;
                return clock == 1
                    ? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    : new DateTimeOffset(2026, 1, 1, 0, 0, 10, TimeSpan.Zero);
            },
            delayAsync: (_, _) =>
            {
                sleeps++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.StartUnconfirmed, result.Action);
        Assert.Equal(1, result.ExitCode);
        Assert.True(result.Started);
        Assert.Equal(1, queries);
        Assert.Equal(0, sleeps);
    }

    [Fact]
    public async Task Process_that_appears_during_the_wait_sleeps_once()
    {
        var queries = 0;
        var sleeps = 0;
        var result = await _ensure.EnsureAsync(
            isRunning: false,
            executableExists: true,
            hasInteractiveSession: true,
            startAsync: _ => Task.CompletedTask,
            queryRunningAsync: _ =>
            {
                queries++;
                return Task.FromResult(queries >= 2);
            },
            wait: TimeSpan.FromSeconds(10),
            getNow: () => new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            delayAsync: (_, _) =>
            {
                sleeps++;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.Started, result.Action);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Started);
        Assert.Equal(2, queries);
        Assert.Equal(1, sleeps);
    }
}
