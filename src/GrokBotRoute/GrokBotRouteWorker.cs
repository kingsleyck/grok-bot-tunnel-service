using Microsoft.Extensions.Options;

namespace GrokBotRoute;

public sealed class GrokBotRouteWorker : BackgroundService
{
    private readonly GrokBotRouteTicker _ticker;
    private readonly IGrokBotRuntime _runtime;
    private readonly IOptionsMonitor<GrokBotRouteOptions> _options;
    private readonly ILogger<GrokBotRouteWorker> _logger;

    public GrokBotRouteWorker(
        GrokBotRouteTicker ticker,
        IGrokBotRuntime runtime,
        IOptionsMonitor<GrokBotRouteOptions> options,
        ILogger<GrokBotRouteWorker> logger)
    {
        _ticker = ticker;
        _runtime = runtime;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var waitingLogged = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await _ticker.TickAsync(_options.CurrentValue, _runtime, stoppingToken)
                    .ConfigureAwait(false);
                waitingLogged = Log(result, waitingLogged);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                waitingLogged = false;
                _logger.LogError(ex, "Grok Bot route check failed.");
            }

            var seconds = _options.CurrentValue.PollIntervalSeconds;
            if (seconds < 1)
            {
                seconds = 30;
            }

            await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken).ConfigureAwait(false);
        }
    }

    private bool Log(GrokBotEnsureResult result, bool waitingLogged)
    {
        switch (result.Action)
        {
            case GrokBotEnsureAction.WaitingForSession:
                if (!waitingLogged)
                {
                    _logger.LogInformation("{Message}", result.Message);
                }

                return true;
            case GrokBotEnsureAction.AlreadyRunning:
                _logger.LogDebug("{Message}", result.Message);
                return false;
            case GrokBotEnsureAction.Started:
                _logger.LogInformation("{Message}", result.Message);
                return false;
            default:
                _logger.LogWarning("{Message}", result.Message);
                return false;
        }
    }
}
