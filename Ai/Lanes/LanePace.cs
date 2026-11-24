namespace BotArena.Ai.Lanes;

public sealed class WaveGroup
{
    public float Progress;
}

public sealed class LanePace
{
    private readonly Dictionary<string, float> measured = [];

    public float Speed { get; private set; } = LaneSettings.PaceSpeed;

    public void Report(string kind, float sample)
    {
        if (sample is < 40 or > 900) return;
        measured[kind] = measured.TryGetValue(kind, out float old) ? old * 0.7f + sample * 0.3f : sample;
        Speed = Math.Clamp(measured.Values.Min() * LaneSettings.PaceFactor, 60f, 700f);
    }

    public string Describe() =>
        $"tempo {Speed:0} cm/s; zmierzone: " + (measured.Count == 0 ? "brak" : string.Join(", ", measured.Select(pair => $"{pair.Key} {pair.Value:0}")));
}
