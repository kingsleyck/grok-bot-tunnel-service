namespace GrokBotRoute;

public enum GrokBotEnsureAction
{
    AlreadyRunning,
    Missing,
    WaitingForSession,
    Start,
    Started,
    StartFailed,
    StartUnconfirmed,
}

public sealed record GrokBotEnsureResult(
    GrokBotEnsureAction Action,
    int ExitCode,
    bool Started,
    string Message);

public sealed class GrokBotEnsure
{
    public GrokBotEnsureResult Plan(bool isRunning, bool executableExists, bool hasInteractiveSession)
    {
        if (isRunning)
        {
            return new GrokBotEnsureResult(
                GrokBotEnsureAction.AlreadyRunning,
                0,
                false,
                "Grok Bot is already running.");
        }

        if (!executableExists)
        {
            return new GrokBotEnsureResult(
                GrokBotEnsureAction.Missing,
                1,
                false,
                "Grok Bot executable is missing.");
        }

        if (!hasInteractiveSession)
        {
            return new GrokBotEnsureResult(
                GrokBotEnsureAction.WaitingForSession,
                0,
                false,
                "Waiting for an interactive user session.");
        }

        return new GrokBotEnsureResult(
            GrokBotEnsureAction.Start,
            0,
            false,
            "Grok Bot will be started.");
    }

    public async Task<GrokBotEnsureResult> EnsureAsync(
        bool isRunning,
        bool executableExists,
        bool hasInteractiveSession,
        Func<CancellationToken, Task> startAsync,
        Func<CancellationToken, Task<bool>> queryRunningAsync,
        TimeSpan wait,
        Func<DateTimeOffset> getNow,
        Func<TimeSpan, CancellationToken, Task> delayAsync,
        CancellationToken cancellationToken)
    {
        var plan = Plan(isRunning, executableExists, hasInteractiveSession);
        if (plan.Action != GrokBotEnsureAction.Start)
        {
            return plan;
        }

        try
        {
            await startAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new GrokBotEnsureResult(
                GrokBotEnsureAction.StartFailed,
                1,
                false,
                "Grok Bot failed to start.");
        }

        var deadline = getNow().ToUniversalTime().Add(wait);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await queryRunningAsync(cancellationToken).ConfigureAwait(false))
            {
                return new GrokBotEnsureResult(
                    GrokBotEnsureAction.Started,
                    0,
                    true,
                    "Grok Bot started.");
            }

            if (getNow().ToUniversalTime() >= deadline)
            {
                return new GrokBotEnsureResult(
                    GrokBotEnsureAction.StartUnconfirmed,
                    1,
                    true,
                    "Grok Bot did not appear after start.");
            }

            await delayAsync(TimeSpan.FromSeconds(1), cancellationToken).ConfigureAwait(false);
        }
    }
}
