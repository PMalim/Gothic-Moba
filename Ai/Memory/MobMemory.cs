namespace BotArena.Ai.Memory;

public interface IPersistentState { }

public sealed class KnownCharacter(Character character)
{
    public Character Character { get; } = character;
    public Position LastSeen { get; private set; }
    public TimeSpan SeenAt { get; private set; }
    public bool Visible { get; internal set; }
    public float VelocityX { get; private set; }
    public float VelocityZ { get; private set; }
    public float Threat { get; internal set; }
    public TimeSpan? UnreachableSince { get; internal set; }
    public bool Grudge { get; internal set; }

    internal void See(Position position, TimeSpan now)
    {
        if (SeenAt > TimeSpan.Zero && now > SeenAt)
        {
            double seconds = (now - SeenAt).TotalSeconds;
            if (seconds is > 0.05 and < 3)
            {
                float vx = (float)((position.X - LastSeen.X) / seconds), vz = (float)((position.Z - LastSeen.Z) / seconds);
                VelocityX = VelocityX * 0.4f + vx * 0.6f;
                VelocityZ = VelocityZ * 0.4f + vz * 0.6f;
            }
        }
        LastSeen = position;
        SeenAt = now;
        Visible = true;
    }

    public TimeSpan Age(TimeSpan now) => now - SeenAt;

    public Position Predict(float seconds, float max)
    {
        float dx = VelocityX * seconds, dz = VelocityZ * seconds, length = MathF.Sqrt(dx * dx + dz * dz);
        if (length > max) { dx *= max / length; dz *= max / length; }
        return new Position(LastSeen.X + dx, LastSeen.Y, LastSeen.Z + dz);
    }
}

public sealed class MobMemory
{
    private readonly Dictionary<Type, object> states = [];
    private readonly Dictionary<Character, KnownCharacter> known = [];
    private readonly Dictionary<Character, TimeSpan> ignored = [];

    public IReadOnlyCollection<KnownCharacter> Known => known.Values;

    public T Get<T>() where T : class, new()
    {
        if (states.TryGetValue(typeof(T), out object? state)) return (T)state;
        var created = new T();
        states[typeof(T)] = created;
        return created;
    }

    public KnownCharacter? Recall(Character? character) => character is not null && known.TryGetValue(character, out var entry) ? entry : null;

    public KnownCharacter Know(Character character)
    {
        if (!known.TryGetValue(character, out KnownCharacter? entry)) known[character] = entry = new KnownCharacter(character);
        return entry;
    }

    public KnownCharacter See(Character character, Position position, TimeSpan now)
    {
        KnownCharacter entry = Know(character);
        entry.See(position, now);
        return entry;
    }

    public void Forget(Character character) => known.Remove(character);

    public void Ignore(Character character, TimeSpan until)
    {
        known.Remove(character);
        ignored[character] = until;
    }

    public bool IsIgnored(Character character, TimeSpan now) => ignored.TryGetValue(character, out TimeSpan until) && now < until;
    public void StopIgnoring(Character character) => ignored.Remove(character);

    internal void BeginSense()
    {
        foreach (KnownCharacter entry in known.Values) entry.Visible = false;
    }

    internal void Prune(Func<Character, bool> present, TimeSpan now, TimeSpan retention)
    {
        foreach (KnownCharacter entry in known.Values.ToArray())
        {
            if (entry.Threat > 0) entry.Threat = Math.Max(0, entry.Threat * 0.99f - 0.5f);
            if (!entry.Character.IsValid || !present(entry.Character) || (!entry.Visible && entry.Age(now) > retention))
                known.Remove(entry.Character);
        }
        foreach (var (character, until) in ignored.ToArray())
            if (now >= until || !character.IsValid) ignored.Remove(character);
    }

    internal void Reset()
    {
        known.Clear();
        ignored.Clear();
        foreach (Type type in states.Keys.ToArray())
            if (states[type] is not IPersistentState) states.Remove(type);
    }

    internal IEnumerable<IPersistentState> PersistentStates => states.Values.OfType<IPersistentState>();
    internal void Restore(IPersistentState state) => states[state.GetType()] = state;
}
