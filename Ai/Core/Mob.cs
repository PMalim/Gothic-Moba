namespace BotArena.Ai;

public enum FocusKind { None, LastSeen, Threat, Home, Wander, Circle, Search }

public enum MobState
{
    Dormant,
    Active,
    Dead,
}

public sealed class Mob(int number, MobKind kind, Camp? camp, int slot, Position home, bool summoned)
{
    public int Number { get; } = number;
    public MobKind Kind { get; } = kind;
    public Camp? Camp { get; } = camp;
    public Position Home { get; internal set; } = home;
    public bool Summoned { get; } = summoned;
    public LaneDef? Lane { get; internal set; }
    public float? Facing { get; internal set; }
    public int DefIndex { get; init; } = -1;
    public WaveGroup? Wave { get; init; }
    public float WaveOffset { get; init; }
    internal bool Listed { get; set; }
    public bool Transient => Summoned || (Lane is not null && !Champion && !Kind.Respawns);
    public int Tier { get; init; }
    public bool Champion { get; init; }
    public int? BotId { get; init; }
    public Player? Owner { get; init; }
    public Mob? Master { get; init; }
    public bool IsHelper => Owner is not null || Master is not null;
    public TimeSpan? ExpiresAt { get; init; }
    public Position? RunTo { get; init; }
    public WeaponMode? StartWeaponMode { get; init; }
    internal TimeSpan ShieldUntil;
    internal float ShieldReduction;
    internal TimeSpan BoostUntil;
    internal float Boost;
    internal TimeSpan HasteUntil;
    internal float Haste;
    internal TimeSpan CcImmuneUntil;
    public int Upgrades { get; init; }
    public int? ScaledMaxHealth { get; internal set; }
    public int? ChampionDamage { get; internal set; }
    public BotArena.Heroes.HeroStats? ChampionStats { get; internal set; }
    public int ChampionLevel { get; internal set; } = 1;
    public bool WantsShop { get; internal set; }
    public int MaxHealth => ScaledMaxHealth ?? Kind.Health;
    public string Label => $"{Kind.DisplayName} #{Number}";
    public string? Key => Camp is null || Summoned ? null : $"{Camp.Name}/{slot}";
    public MobBehaviour Behaviour => Kind.Behaviour;

    public Npc? Npc { get; internal set; }
    public Player? Host { get; internal set; }
    public MobState State { get; internal set; } = MobState.Dead;
    public Character? Target { get; internal set; }
    public Character? Quarry { get; internal set; }
    public MobMemory Memory { get; } = new();
    public BehaviorMemory? Tree { get; internal set; }
    public string Activity => State switch
    {
        MobState.Dormant => "brak hosta: żaden gracz go nie streamuje",
        MobState.Dead => Note,
        _ => Note.Length > 0 ? Note : Tree?.ActiveLeaf ?? "",
    };
    public string Note { get; internal set; } = "";

    internal int? CurrentAction;
    private readonly Dictionary<int, bool> finished = [];

    public Position LastPosition { get; internal set; }
    public int LastHealth { get; internal set; }
    public int LastMaxHealth { get; internal set; }
    public int QueuedActions { get; internal set; }
    public Position? Focus { get; internal set; }
    public FocusKind FocusKind { get; internal set; }
    public int FlankAngle { get; internal set; }

    internal void SetFocus(Position? point, FocusKind kind) { Focus = point; FocusKind = point is null ? FocusKind.None : kind; }

    internal bool HadHost;
    internal bool WeaponOut;
    internal TimeSpan LastCombat;
    internal TimeSpan? LastHurt;
    internal bool HurtWithin(TimeSpan window, TimeSpan now) => LastHurt is { } hurt && now - hurt <= window;
    internal TimeSpan LastThink;
    internal TimeSpan? LastHitReaction;
    internal TimeSpan? QueueBusySince;
    internal TimeSpan RespawnAt;
    internal bool DeathReported;
    internal Character? Killer;
    internal int AuthHealth;
    internal TimeSpan StunnedUntil;

    internal void ActionFinished(int action, bool success)
    {
        if (finished.Count > 32) finished.Clear();
        finished[action] = success;
    }
    internal bool TakeFinished(int action, out bool success) => finished.Remove(action, out success);

    internal void ResetForSpawn(BehaviorMemory tree, Position at, int health)
    {
        Tree = tree;
        Memory.Reset();
        State = MobState.Active;
        Target = null;
        Quarry = null;
        finished.Clear();
        CurrentAction = null;
        HadHost = false;
        WeaponOut = false;
        LastHurt = null;
        QueueBusySince = null;
        DeathReported = false;
        Killer = null;
        ShieldUntil = default;
        BoostUntil = default;
        HasteUntil = default;
        CcImmuneUntil = default;
        StunnedUntil = default;
        Note = "";
        FlankAngle = 0;
        LastPosition = at;
        LastHealth = health;
        LastMaxHealth = MaxHealth;
        SetFocus(null, FocusKind.None);
    }
}
