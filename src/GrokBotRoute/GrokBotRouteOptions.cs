namespace GrokBotRoute;

public sealed class GrokBotRouteOptions
{
    public const string SectionName = "GrokBotRoute";

    public string ExecutablePath { get; set; } =
        @"C:\Users\kings\AppData\Local\Programs\Grok Bot\Grok Bot.exe";

    public int PollIntervalSeconds { get; set; } = 30;

    public int StartWaitSeconds { get; set; } = 10;

    public string TargetUserId { get; set; } = string.Empty;
}
