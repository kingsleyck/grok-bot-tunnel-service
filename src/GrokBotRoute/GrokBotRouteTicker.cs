namespace GrokBotRoute;

public sealed class GrokBotRouteTicker
{
    private readonly GrokBotEnsure _ensure;

    public GrokBotRouteTicker(GrokBotEnsure ensure)
    {
        _ensure = ensure;
    }

    public Task<GrokBotEnsureResult> TickAsync(
        GrokBotRouteOptions options,
        IGrokBotRuntime runtime,
        CancellationToken cancellationToken)
    {
        var path = options.ExecutablePath;
        var exists = runtime.ExecutableExists(path);
        var running = exists && runtime.IsRunning(path);
        var session = runtime.HasInteractiveSession(options.TargetUserId);
        var waitSeconds = options.StartWaitSeconds > 0 ? options.StartWaitSeconds : 10;

        return _ensure.EnsureAsync(
            running,
            exists,
            session,
            _ =>
            {
                runtime.Start(path, options.TargetUserId);
                return Task.CompletedTask;
            },
            _ => Task.FromResult(runtime.IsRunning(path)),
            TimeSpan.FromSeconds(waitSeconds),
            () => DateTimeOffset.UtcNow,
            Task.Delay,
            cancellationToken);
    }
}
