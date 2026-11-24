namespace BotArena;

public static class AutoAttackGate
{
    private sealed class Entry { public TimeSpan LastHit; public TimeSpan LastReject; public int Rejects; }

    private static readonly Dictionary<int, Entry> Players = [];

    public static bool Allow(int attackerId, TimeSpan now, out int suspicious)
    {
        suspicious = 0;
        if (!Players.TryGetValue(attackerId, out Entry? entry)) Players[attackerId] = entry = new Entry { LastHit = TimeSpan.MinValue / 2 };
        if (now - entry.LastHit >= LaneSettings.AutoAttackGap)
        {
            entry.LastHit = now;
            return true;
        }
        entry.Rejects = now - entry.LastReject <= TimeSpan.FromSeconds(5) ? entry.Rejects + 1 : 1;
        entry.LastReject = now;
        if (entry.Rejects == 5 || (entry.Rejects > 5 && entry.Rejects % 25 == 0)) suspicious = entry.Rejects;
        return false;
    }

    public static void Forget(int attackerId) => Players.Remove(attackerId);
}
