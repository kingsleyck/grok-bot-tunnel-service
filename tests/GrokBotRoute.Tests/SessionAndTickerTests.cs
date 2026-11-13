namespace GrokBotRoute.Tests;

public sealed class InteractiveSessionPickerTests
{
    [Fact]
    public void Login_screen_has_no_session()
    {
        var chosen = InteractiveSessionPicker.Choose(
            [new InteractiveSession(0, "Active", "", "")],
            string.Empty,
            0);
        Assert.Null(chosen);
    }

    [Fact]
    public void Active_console_session_is_preferred()
    {
        var sessions = new[]
        {
            new InteractiveSession(2, "Active", "PC", "other"),
            new InteractiveSession(1, "Active", "PC", "kings"),
        };
        var chosen = InteractiveSessionPicker.Choose(sessions, string.Empty, 1);
        Assert.Equal(1u, chosen);
    }

    [Fact]
    public void Target_user_must_match()
    {
        var sessions = new[]
        {
            new InteractiveSession(1, "Active", "PC", "kings"),
        };
        Assert.Equal(1u, InteractiveSessionPicker.Choose(sessions, @"PC\kings", null));
        Assert.Null(InteractiveSessionPicker.Choose(sessions, @"PC\other", null));
    }

    [Fact]
    public void Disconnected_session_is_ignored()
    {
        var chosen = InteractiveSessionPicker.Choose(
            [new InteractiveSession(1, "Disconnected", "PC", "kings")],
            string.Empty,
            1);
        Assert.Null(chosen);
    }
}

public sealed class GrokBotRouteTickerTests
{
    [Fact]
    public async Task No_session_does_not_start()
    {
        var runtime = new FakeRuntime { Session = false };
        var ticker = new GrokBotRouteTicker(new GrokBotEnsure());
        var result = await ticker.TickAsync(new GrokBotRouteOptions(), runtime, CancellationToken.None);

        Assert.Equal(GrokBotEnsureAction.WaitingForSession, result.Action);
        Assert.Equal(0, runtime.Starts);
    }

    private sealed class FakeRuntime : IGrokBotRuntime
    {
        public bool Session { get; init; }

        public int Starts { get; private set; }

        public bool ExecutableExists(string path) => true;

        public bool IsRunning(string path) => false;

        public bool HasInteractiveSession(string targetUserId) => Session;

        public void Start(string path, string targetUserId) => Starts++;
    }
}
