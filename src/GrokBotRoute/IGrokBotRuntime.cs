namespace GrokBotRoute;

public interface IGrokBotRuntime
{
    bool ExecutableExists(string path);

    bool IsRunning(string path);

    bool HasInteractiveSession(string targetUserId);

    void Start(string path, string targetUserId);
}
