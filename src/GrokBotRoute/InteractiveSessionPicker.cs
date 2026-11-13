namespace GrokBotRoute;

public readonly record struct InteractiveSession(
    uint Id,
    string State,
    string Domain,
    string User);

public static class InteractiveSessionPicker
{
    public static uint? Choose(
        IReadOnlyList<InteractiveSession> sessions,
        string targetUserId,
        uint? preferredId)
    {
        var usable = new List<InteractiveSession>();
        foreach (var session in sessions)
        {
            if (session.Id == 0)
            {
                continue;
            }

            if (!string.Equals(session.State, "Active", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(session.State, "Connected", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!MatchesUser(session, targetUserId))
            {
                continue;
            }

            usable.Add(session);
        }

        if (usable.Count == 0)
        {
            return null;
        }

        if (preferredId is uint preferred)
        {
            foreach (var session in usable)
            {
                if (session.Id == preferred)
                {
                    return session.Id;
                }
            }
        }

        return usable[0].Id;
    }

    private static bool MatchesUser(InteractiveSession session, string targetUserId)
    {
        if (string.IsNullOrWhiteSpace(targetUserId))
        {
            return true;
        }

        var account = session.Domain + "\\" + session.User;
        return string.Equals(account, targetUserId.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
