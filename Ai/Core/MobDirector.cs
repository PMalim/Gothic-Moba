namespace BotArena.Ai;

public sealed class MobDirector
{
    private readonly Server server;
    private readonly VirtualWorldId world;
    private bool disposed;
    private readonly MobBrain brain;
    private readonly MemoryStore? store;
    private readonly List<Mob> mobs = [];
    private readonly Dictionary<Npc, Mob> byNpc = [];
    private readonly Dictionary<int, Mob> byId = [];
    private readonly List<Mob> structures = [];
    private readonly List<IDisposable> subscriptions = [];
    private IReadOnlyDictionary<Character, Combatant> lastIndex = new Dictionary<Character, Combatant>();
    private TimeSpan nextPrune;
    private IReadOnlyList<Combatant> lastPlayers = [];
    private IReadOnlyList<Combatant> lastMobs = [];
    private int nextNumber;
    private readonly HashSet<Mob> woken = [];
    private bool wakeScheduled;
    private static readonly TimeSpan EventThinkGap = TimeSpan.FromMilliseconds(120);

    public event Action? PopulationChanged;
    public event Action<Mob, Character?>? MobKilled;
    public event Action<Mob>? TurretSpawned;
    public event Action<Mob>? ObjectiveSpawned;
    public event Action<int>? TurretRemoved;
    public event Action<Mob>? StructureRebuilt;
    public event Action<Player, Mob>? HitBlocked;
    public event Action<Mob, Character>? TurretShot;

    public event Action<Mob, Character>? MinionShot;

    public event Action<Mob, Character?, int, string>? ChampionFx;

    public void ChampionEffect(Mob caster, Character? target, int kind, string fx) => ChampionFx?.Invoke(caster, target, kind, fx);
    public event Action<Mob, string, int>? ChampionAuraFx;
    public void ChampionAura(Mob caster, string fx, int millis) { if (fx.Length > 0) ChampionAuraFx?.Invoke(caster, fx, millis); }
    public event Action<Mob, string>? MobAnimation;
    public void Animate(Mob mob, string animation) => MobAnimation?.Invoke(mob, animation);

    private readonly List<(Character Target, int Amount, TimeSpan Due, Character? Attacker, bool Magic)> pendingDamage = [];

    public Func<Player, bool, float>? Mitigation { get; set; }
    public Func<Player, float>? PlayerShield { get; set; }
    public Func<Player, bool>? CritRoll { get; set; }
    public Func<Player, float>? CritMultiplier { get; set; }
    public Func<Player, bool>? BasicMagic { get; set; }
    public Func<Player, Character, int, int>? PlayerHit { get; set; }
    public Func<Player, int, int>? BasicDamage { get; set; }
    public Func<Mob, bool, float>? ChampionMitigation { get; set; }
    private readonly Dictionary<PlayerId, (Character Attacker, TimeSpan At)> lastPlayerAttacker = [];

    public Mob? MobOf(Character? character) => character is Npc npc && byNpc.TryGetValue(npc, out Mob? mob) ? mob : null;

    public Character? RecentAttackerOf(Player victim) =>
        lastPlayerAttacker.TryGetValue(victim.Id, out var hit) && server.Clock.Elapsed - hit.At < TimeSpan.FromSeconds(10) && hit.Attacker.IsValid ? hit.Attacker : null;

    public void Heal(Mob mob, int amount)
    {
        if (mob.Npc is not { IsValid: true } npc || npc.IsDead || amount <= 0) return;
        int max = mob.MaxHealth;
        SetHealth(npc, Math.Min(max, mob.AuthHealth + amount));
    }

    public void SetChampionPower(Mob mob, int maxHealth, int damage, BotArena.Heroes.HeroStats stats, int level)
    {
        int before = mob.MaxHealth;
        mob.ScaledMaxHealth = Math.Max(1, maxHealth);
        mob.ChampionDamage = Math.Max(1, damage);
        mob.ChampionStats = stats;
        mob.ChampionLevel = level;
        if (mob.State == MobState.Dead || mob.Npc is not { IsValid: true } npc || npc.IsDead) return;
        npc.MaxHealth = mob.MaxHealth;
        SetHealth(npc, Math.Clamp(mob.AuthHealth + Math.Max(0, mob.MaxHealth - before), 1, mob.MaxHealth));
    }

    public void Shield(Mob mob, TimeSpan until, float reduction)
    {
        mob.ShieldUntil = until;
        mob.ShieldReduction = Math.Clamp(reduction, 0f, 1f);
    }

    private static int Shielded(Mob? victim, int amount, TimeSpan now) =>
        victim is not null && now < victim.ShieldUntil ? (int)Math.Round(amount * (1f - victim.ShieldReduction)) : amount;

    private int Incoming(Mob? victim, Character? attacker, int amount, bool magic, TimeSpan now)
    {
        if (victim is null) return amount;
        amount = Shielded(victim, amount, now);
        float factor = 1f;
        foreach (MobComponent component in victim.Behaviour.Components) factor *= component.IncomingDamage(victim, attacker, now);
        if (victim.Lane is not null && !victim.Champion && !victim.Kind.Immobile && IsEmpowered(victim.Kind.Faction, now)
            && (attacker is Player || MobOf(attacker) is { Champion: true }))
            factor *= 1f - BaronSettings.EmpoweredResist;
        int defence = magic ? victim.Kind.ResistValue : victim.Kind.ArmorValue;
        if (!victim.Champion && !victim.Kind.Immobile && defence > 0) factor *= BotArena.Heroes.HeroResources.Mitigation(defence);
        return factor == 1f ? amount : (int)Math.Round(amount * factor);
    }

    public Dictionary<Faction, TimeSpan> EmpoweredUntil { get; } = [];
    public bool IsEmpowered(Faction team, TimeSpan now) => EmpoweredUntil.TryGetValue(team, out TimeSpan until) && now < until;

    public Func<Player, bool, int, float>? ShreddedMitigation { get; set; }
    public Action<Player, TimeSpan>? HeroFreeze { get; set; }
    public Action<Player, float, int>? HeroSlow { get; set; }
    public Action<Character, Mob?, Position, TimeSpan>? DragTo { get; set; }
    public event Action<Mob, Position, string>? GroundFx;
    public void GroundEffect(Mob caster, Position at, string fx) => GroundFx?.Invoke(caster, at, fx);

    private readonly Dictionary<Character, (int Stacks, TimeSpan Until)> corruption = [];

    public void AddCorruption(Character who, int stacks, TimeSpan now)
    {
        int had = CorruptionOf(who, now);
        corruption[who] = (Math.Min(BaronSettings.MaxCorruption, had + stacks), now + BaronSettings.CorruptionLength);
    }

    public int CorruptionOf(Character who, TimeSpan now)
    {
        if (!corruption.TryGetValue(who, out var entry)) return 0;
        if (now >= entry.Until || !who.IsValid) { corruption.Remove(who); return 0; }
        return entry.Stacks;
    }

    public int HealthOf(Character who) => MobOf(who) is { } mob ? mob.AuthHealth : who.Health;

    public void Immobilize(Character who, TimeSpan until)
    {
        if (MobOf(who) is { } mob) { if (!mob.Kind.Immobile) Stun(mob, until); }
        else if (who is Player player) HeroFreeze?.Invoke(player, until);
    }

    public void Slow(Character who, float amount, TimeSpan until)
    {
        if (who is Player player) HeroSlow?.Invoke(player, 1f - amount, (int)Math.Max(100, (until - server.Clock.Elapsed).TotalMilliseconds));
    }

    public void Pull(Character who, Position to, TimeSpan duration)
    {
        if (MobOf(who) is { Kind.Immobile: true }) return;
        DragTo?.Invoke(who, MobOf(who), to, duration);
    }

    public void ScheduleDamage(Character target, int amount, TimeSpan due, Character? attacker = null, bool magic = false) => pendingDamage.Add((target, amount, due, attacker, magic));

    public void BoltFire(Mob caster, Character target, int damage, TimeSpan now, float speed = LaneSettings.BoltSpeed)
    {
        MinionShot?.Invoke(caster, target);
        float distance = caster.LastPosition.DistanceTo(target.Position).Centimeters;
        TimeSpan flight = TimeSpan.FromSeconds(Math.Clamp(distance / speed, 0.1f, 1.5f));
        ScheduleDamage(target, damage, now + LaneSettings.BoltCastDelay + flight, caster.Npc, magic: false);
    }

    public void RayFire(Mob dragon, Character target, TimeSpan now)
    {
        TurretShot?.Invoke(dragon, target);
        ScheduleDamage(target, LaneSettings.DragonDamage, now + LaneSettings.DragonShotDelay, magic: false);
    }

    private bool StructureAlive(Mob mob) => mob.Npc is { IsValid: true } && mob.State != MobState.Dead && !mob.DeathReported;

    public bool InhibitorDown(Faction team)
    {
        foreach (Mob m in structures)
            if (m.Kind.Structure == StructureKind.Inhibitor && m.Kind.Faction == team && !StructureAlive(m)) return true;
        return false;
    }

    public bool IsProtected(Mob mob)
    {
        if (!mob.Kind.Immobile) return false;
        Faction team = mob.Kind.Faction;
        switch (mob.Kind.Structure)
        {
            case StructureKind.Dragon: return true;
            case StructureKind.Turret when mob.Tier == 0: return !InhibitorDown(team);
            case StructureKind.Turret or StructureKind.Inhibitor or StructureKind.Nexus:
                foreach (Mob m in structures)
                {
                    if (m.Kind.Faction != team || ReferenceEquals(m, mob) || m.Kind.Structure != StructureKind.Turret || !StructureAlive(m)) continue;
                    bool holds = mob.Kind.Structure switch
                    {
                        StructureKind.Turret => m.Tier > 0 && m.Tier < mob.Tier && m.Lane == mob.Lane,
                        StructureKind.Inhibitor => m.Tier > 0 && m.Lane == mob.Lane,
                        _ => m.Tier == 0,
                    };
                    if (holds) return true;
                }
                return false;
            default: return false;
        }
    }

    public static string ProtectionHint(Mob mob) => mob.Kind.Structure switch
    {
        StructureKind.Dragon => "Smoka nie da się zniszczyć — omijaj jego zasięg.",
        StructureKind.Turret when mob.Tier > 0 => "Ta wieża jest chroniona: najpierw zniszcz wcześniejsze wieże tej linii.",
        StructureKind.Turret => "Wieże nexusa są chronione, dopóki stoi inhibitor.",
        StructureKind.Inhibitor => "Inhibitor jest chroniony: najpierw zniszcz wszystkie wieże tej linii.",
        StructureKind.Nexus => "Nexus jest chroniony: najpierw zniszcz obie wieże nexusa.",
        _ => "Ta struktura jest chroniona.",
    };

    public void TurretFire(Mob turret, Character target, int damage, TimeSpan now)
    {
        TurretShot?.Invoke(turret, target);
        ScheduleDamage(target, damage, now + LaneSettings.TurretShotDelay, magic: false);
    }

    private void ApplyDueDamage(TimeSpan now)
    {
        for (int i = pendingDamage.Count - 1; i >= 0; i--)
        {
            var (target, amount, due, attacker, magic) = pendingDamage[i];
            if (due > now) continue;
            pendingDamage.RemoveAt(i);
            try
            {
                if (!target.IsValid || target.IsDead) continue;
                if (target is Player gone && gone.VirtualWorld != world) continue;
                Mob? scheduledMob = null;
                bool isMob = target is Npc scheduled && byNpc.TryGetValue(scheduled, out scheduledMob);
                if (isMob && IsProtected(scheduledMob!)) continue;
                if (isMob) amount = Incoming(scheduledMob, attacker, amount, magic, now);
                if (isMob && scheduledMob!.Champion && ChampionMitigation is { } armour) amount = (int)Math.Round(amount * armour(scheduledMob, magic));
                else if (target is Player armoured && ShreddedMitigation is { } shredded && CorruptionOf(armoured, now) is > 0 and var stacks)
                    amount = (int)Math.Round(amount * shredded(armoured, magic, (int)(stacks * BaronSettings.CorruptionPerStack)));
                else if (target is Player armoured2 && Mitigation is { } mitigate) amount = (int)Math.Round(amount * mitigate(armoured2, magic));
                if (target is Player shieldedPlayer && PlayerShield is { } shield) amount = (int)Math.Round(amount * shield(shieldedPlayer));
                if (target is Player hitPlayer && attacker is not null) lastPlayerAttacker[hitPlayer.Id] = (attacker, now);
                int after = Math.Max(0, (isMob ? scheduledMob!.AuthHealth : target.Health) - amount);
                if (isMob && after == 0 && attacker is not null) scheduledMob!.Killer = attacker;
                SetHealth(target, after);
                if (isMob && attacker is not null) OnDamaged(target, attacker, amount, magic);
                else RecordAggression(attacker, target, amount, magic);
            }
            catch (Exception error) { server.Log($"obrażenia: {error.Message}"); }
        }
    }

    public MobDirector(Server server, int seed, MemoryStore? store = null, VirtualWorldId? world = null)
    {
        this.server = server;
        this.store = store;
        this.world = world ?? VirtualWorldId.Default;
        brain = new MobBrain(new MobCommands(server), new Random(seed), this);
        void On<T>(ServerEvent<T> source, Action<T> handler) => subscriptions.Add(source.Subscribe(handler));
        On(server.Events.CharacterDamaged, e =>
        {
            if (disposed) return;
            FixDamage(e);
            OnDamaged(e.Victim, e.Attacker, e.Amount, e.Description.SpellId is not null);
        });
        On(server.Events.NpcActionCompleted, e =>
        {
            if (disposed || !byNpc.TryGetValue(e.Npc, out Mob? mob)) return;
            mob.ActionFinished(e.Action.Value, e.Success);
            Wake(mob);
        });
        On(server.Events.HealthChanged, e =>
        {
            if (disposed || e.Character is not Npc npc || !byNpc.TryGetValue(npc, out Mob? mob)) return;
            if (mob.Lane is null)
            {
                if (e.Current < mob.AuthHealth) mob.AuthHealth = Math.Max(0, e.Current);
                return;
            }
            if (e.Current >= mob.AuthHealth) return;
            if (e.Current <= 0 || mob.AuthHealth <= 0) return;
            int confirmed = mob.AuthHealth;
            server.Post(_ => { if (npc.IsValid && !npc.IsDead && mob.AuthHealth == confirmed) npc.Health = confirmed; });
        });
        On(server.Events.CharacterDied, e =>
        {
            if (!disposed && e.Victim is Npc npc && byNpc.TryGetValue(npc, out Mob? mob))
            {
                mob.DeathReported = true;
                if (e.Killer is not null) mob.Killer = e.Killer;
            }
        });
    }

    public IReadOnlyList<Mob> Mobs => mobs;

    public event Action<Character, Character, int, bool>? DamageDealt;

    private readonly List<(Character Attacker, Character Victim, TimeSpan At)> aggression = [];
    private static readonly TimeSpan AggressionMemory = TimeSpan.FromSeconds(3);

    private bool IsChampion(Character who) => who is Player || MobOf(who) is { Champion: true };

    public void RecordAggression(Character? attacker, Character victim, int amount, bool magic = false)
    {
        if (attacker is not null && amount > 0)
        {
            NoteAssist(attacker, victim, server.Clock.Elapsed);
            if (!ReferenceEquals(attacker, victim))
                DamageDealt?.Invoke(MasterOf(attacker) ?? attacker, victim, amount, magic);
        }
        if (attacker is null || amount <= 0 || ReferenceEquals(attacker, victim) || !IsChampion(attacker) || !IsChampion(victim)) return;
        TimeSpan now = server.Clock.Elapsed;
        aggression.RemoveAll(a => now - a.At > AggressionMemory);
        aggression.Add((attacker, victim, now));
    }

    public sealed record AssistHit(PlayerId? Player, int? BotId, Faction Team, TimeSpan At);

    private readonly Dictionary<object, Dictionary<object, AssistHit>> assistLedger = [];

    private object? LedgerKey(Character who) => who is Player player ? player.Id : MobOf(who) is { Champion: true } mob ? mob : null;

    private void NoteAssist(Character attacker, Character victim, TimeSpan now)
    {
        if (MasterOf(attacker) is { } master) attacker = master;
        if (ReferenceEquals(attacker, victim) || LedgerKey(victim) is not { } victimKey || LedgerKey(attacker) is not { } attackerKey) return;
        Faction victimTeam = victim is Player victimPlayer ? Teams.Of(victimPlayer) : MobOf(victim)!.Kind.Faction;
        AssistHit hit = attacker is Player player ? new(player.Id, null, Teams.Of(player), now) : new(null, MobOf(attacker)!.BotId, MobOf(attacker)!.Kind.Faction, now);
        if (hit.Team == victimTeam) return;
        if (!assistLedger.TryGetValue(victimKey, out Dictionary<object, AssistHit>? hits)) assistLedger[victimKey] = hits = [];
        hits[attackerKey] = hit;
    }

    public IReadOnlyList<AssistHit> TakeAssists(Player victim) => Take(victim.Id);

    public IReadOnlyList<AssistHit> TakeAssists(Mob victim) => Take(victim);

    private IReadOnlyList<AssistHit> Take(object key)
    {
        if (!assistLedger.Remove(key, out Dictionary<object, AssistHit>? hits)) return [];
        TimeSpan now = server.Clock.Elapsed;
        return [.. hits.Values.Where(hit => now - hit.At <= BotArena.Matches.MatchService.AssistWindow)];
    }

    public IEnumerable<(Character Attacker, Character Victim, TimeSpan At)> RecentAggression(TimeSpan now, TimeSpan window)
    {
        for (int i = aggression.Count - 1; i >= 0; i--)
            if (now - aggression[i].At <= window) yield return aggression[i];
    }
    public LanePace Pace { get; } = new();

    public void Log(string message) => server.Log(message);
    public int Alive => mobs.Count(mob => mob.Npc is not null && mob.Lane is null && !mob.IsHelper);
    public int Total => mobs.Count(mob => mob.Lane is null && !mob.IsHelper);

    public Mob SpawnCompanion(MobKind kind, Player owner, Position at, TimeSpan now, TimeSpan life, int limit, Position? runTo = null, WeaponMode? weaponMode = null)
    {
        Mob[] same = [.. mobs.Where(m => m.Owner is { } o && o.Id == owner.Id && m.Kind.Key == kind.Key && m.Npc is not null && !m.DeathReported)];
        for (int i = 0; i <= same.Length - limit; i++) Retire(same[i], "zastąpiony nowym");
        var mob = new Mob(++nextNumber, kind, null, 0, at, summoned: true)
        {
            Owner = owner, ExpiresAt = now + life, RunTo = runTo, StartWeaponMode = weaponMode,
        };
        Spawn(mob, now);
        return mob;
    }

    public Mob SpawnHelper(MobKind kind, Mob master, Position at, TimeSpan now, TimeSpan life, int limit, Position? runTo = null, WeaponMode? weaponMode = null)
    {
        Mob[] same = [.. mobs.Where(m => ReferenceEquals(m.Master, master) && m.Kind.Key == kind.Key && m.Npc is not null && !m.DeathReported)];
        for (int i = 0; i <= same.Length - limit; i++) Retire(same[i], "zastąpiony nowym");
        var mob = new Mob(++nextNumber, kind, null, 0, at, summoned: true)
        {
            Master = master, ExpiresAt = now + life, RunTo = runTo, StartWeaponMode = weaponMode,
        };
        Spawn(mob, now);
        return mob;
    }

    public Character? MasterOf(Character? who) => MobOf(who) switch
    {
        { Owner: { IsValid: true } player } => player,
        { Master.Npc: { IsValid: true } npc } => npc,
        _ => null,
    };

    public void RetireOwned(Player owner)
    {
        foreach (Mob mob in mobs.ToArray())
            if (mob.Owner is { } o && o.Id == owner.Id && mob.Npc is not null) Retire(mob, "pan odszedł");
    }

    public TimeSpan Started { get; set; }
    public int UpgradesAt(TimeSpan now) => Started == default ? 0 : (int)Math.Max(0, (now - Started).TotalSeconds / MinionStats.UpgradeSeconds);
    public double GameMinutes(TimeSpan now) => Started == default ? 0 : Math.Max(0, (now - Started).TotalMinutes);

    public int AttackDamage(Mob attacker, Character victim)
    {
        if (attacker.Kind.Role is not { } role)
        {
            int flat = attacker.ChampionDamage ?? attacker.Kind.Damage ?? 0;
            if (attacker.Champion && server.Clock.Elapsed < attacker.BoostUntil) flat = (int)Math.Round(flat * (1f + attacker.Boost));
            return attacker.Champion && MobOf(victim) is { Kind.Immobile: true } structure
                ? (int)Math.Round(flat * BotArena.Heroes.HeroResources.Mitigation(MinionStats.StructureArmor(structure.Kind.Structure))) : flat;
        }
        float ad = MinionStats.AdAt(role, attacker.Upgrades);
        float empowered = IsEmpowered(attacker.Kind.Faction, server.Clock.Elapsed) ? BaronSettings.EmpoweredDamage : 1f;
        if (MobOf(victim) is { Lane: not null, Champion: false, Kind.Immobile: false } minion)
            return Math.Max(1, (int)Math.Round((ad + minion.AuthHealth * MinionStats.VsMinionBonus(role)) * empowered));
        return Math.Max(1, (int)Math.Round(ad * MinionStats.StructureChampionFactor * empowered));
    }

    public Mob SpawnMinion(MobKind kind, LaneDef lane, Position at, TimeSpan now, WaveGroup? wave = null, float waveOffset = 0)
    {
        int upgrades = kind.Role is null ? 0 : UpgradesAt(now);
        int? scaledHealth = kind.Role is { } role ? MinionStats.HealthAt(role, upgrades) : null;
        var mob = new Mob(++nextNumber, kind, null, 0, at, summoned: false)
            { Lane = lane, Wave = wave, WaveOffset = waveOffset, Upgrades = upgrades, ScaledMaxHealth = scaledHealth };
        Spawn(mob, now);
        return mob;
    }

    public Mob SpawnChampion(MobKind kind, LaneDef lane, Position at, TimeSpan now, int botId)
    {
        var mob = new Mob(++nextNumber, kind, null, 0, at, summoned: false) { Lane = lane, Champion = true, BotId = botId };
        Spawn(mob, now);
        return mob;
    }

    public bool MoveStructure(LaneDef lane, int defIndex, TurretDef def)
    {
        bool found = false;
        foreach (Mob mob in structures.ToArray())
        {
            if (!ReferenceEquals(mob.Lane, lane) || mob.DefIndex != defIndex) continue;
            found = true;
            mob.Home = def.At;
            mob.Facing = def.Angle;
            mob.LastPosition = def.At;
            if (mob.Npc is { IsValid: true } npc && !npc.IsDead)
            {
                npc.Position = def.At;
                npc.SetAngle(new Angle(def.Angle), false);
            }
        }
        return found;
    }

    public int CreateTurrets(TimeSpan now)
    {
        int created = 0;
        if (!LaneSettings.Enabled || !LaneSettings.TurretsEnabled) return 0;
        foreach (LaneDef lane in LaneSettings.Lanes)
            for (int defIndex = 0; defIndex < lane.Turrets.Count; defIndex++)
            {
                TurretDef turret = lane.Turrets[defIndex];
                var mob = new Mob(++nextNumber, StructureKinds.Of(turret), null, 0, turret.At, summoned: false) { Lane = lane, Facing = turret.Angle, Tier = turret.Tier, DefIndex = defIndex };
                Spawn(mob, now);
                if (mob.Npc is null) continue;
                created++;
                TurretSpawned?.Invoke(mob);
            }
        server.Log($"BotArena: wież: {created}.");
        return created;
    }

    public int KillMobs()
    {
        int killed = 0;
        foreach (Mob mob in mobs.ToArray())
        {
            if (mob.Npc is null || mob.Kind.Immobile) continue;
            Retire(mob, "zabity przez admina");
            killed++;
        }
        return killed;
    }

    public void Retire(Mob mob, string reason)
    {
        mob.DeathReported = true;
        mob.Note = reason;
    }

    public int CreateCamps(TimeSpan now)
    {
        int restored = 0;
        foreach (Camp camp in ArenaSettings.Camps)
            for (int i = 0; i < camp.Members.Length; i++)
            {
                var mob = new Mob(++nextNumber, camp.Members[i], camp, i, camp.HomeOf(ArenaSettings.Center, i), summoned: false);
                MobSnapshot? snapshot = mob.Key is { } key ? store?.Take(key) : null;
                if (snapshot is null)
                {
                    if ((mob.Kind.FirstSpawn ?? camp.FirstSpawn) is { } first && first > TimeSpan.Zero)
                    {
                        Enlist(mob);
                        mob.State = MobState.Dead;
                        mob.RespawnAt = now + first;
                        mob.Note = $"pojawi się za {first.TotalSeconds:0} s";
                    }
                    else Spawn(mob, now);
                    continue;
                }
                restored++;
                MemoryStore.RestoreStates(snapshot, mob.Memory);
                bool body = store!.Age is { } age && age <= ArenaSettings.RestoreBodyWithin;
                if (body && snapshot.RespawnIn >= 0)
                {
                    Enlist(mob);
                    mob.State = MobState.Dead;
                    mob.RespawnAt = now + TimeSpan.FromSeconds(snapshot.RespawnIn);
                    mob.Note = "zginął przed przeładowaniem";
                }
                else if (body) Spawn(mob, now, new Position(snapshot.X, snapshot.Y, snapshot.Z), snapshot.Health);
                else Spawn(mob, now);
            }
        server.Log($"BotArena: {Alive}/{Total} mobów w {ArenaSettings.Camps.Length} obozach" +
            (restored > 0 ? $", {restored} z pamięci" : "") + $"; AI co {ArenaSettings.ThinkInterval.TotalMilliseconds} ms.");
        PopulationChanged?.Invoke();
        return restored;
    }

    public int Summon(MobKind kind, Position near, int count, TimeSpan now)
    {
        int room = ArenaSettings.MaxSummoned - mobs.Count(mob => mob.Summoned);
        count = Math.Clamp(count, 0, room);
        for (int i = 0; i < count; i++)
        {
            float angle = i * MathF.Tau / Math.Max(count, 1);
            var home = new Position(near.X + 400 + MathF.Cos(angle) * 150, near.Y, near.Z + MathF.Sin(angle) * 150);
            Spawn(new Mob(++nextNumber, kind, null, i, home, summoned: true), now);
        }
        PopulationChanged?.Invoke();
        return count;
    }

    public void Reset(TimeSpan now)
    {
        RemoveAll();
        CreateCamps(now);
        CreateTurrets(now);
    }

    public void SaveMemory(TimeSpan now)
    {
        if (store is null) return;
        Mob[] living = [.. mobs.Where(mob => mob.Npc is { IsValid: true })];
        IReadOnlyList<NpcState> states = server.Npcs.Snapshot(living.Select(mob => mob.Npc!));
        for (int i = 0; i < living.Length; i++)
        {
            if (!states[i].Exists) continue;
            living[i].LastPosition = states[i].Position;
            living[i].LastHealth = states[i].Health;
        }
        store.Save(mobs, now);
    }

    public void Stun(Mob mob, TimeSpan until)
    {
        if (mob.Behaviour.Has<BaronCombat>()) return;
        if (server.Clock.Elapsed < mob.CcImmuneUntil) return;
        mob.StunnedUntil = until;
        server.Post(_ =>
        {
            if (mob.Npc is not { IsValid: true } npc) return;
            npc.Actions.Clear();
            mob.CurrentAction = null;
        });
    }

    public void Dispose()
    {
        disposed = true;
        foreach (IDisposable subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear();
        RemoveAll();
    }

    public void RemoveAll()
    {
        foreach (Mob mob in mobs) { Despawn(mob); mob.Listed = false; }
        mobs.Clear();
        structures.Clear();
        byNpc.Clear();
        byId.Clear();
        slowed.Clear();
    }

    public IEnumerable<Mob> CampMates(Mob mob, Distance range)
    {
        if (mob.Camp is null) yield break;
        foreach (Mob mate in mobs)
            if (!ReferenceEquals(mate, mob) && mate.Camp == mob.Camp && mate.Npc is not null && mate.State != MobState.Dead
                && mate.LastPosition.DistanceTo(mob.LastPosition) <= range)
                yield return mate;
    }

    private readonly HashSet<Mob> slowed = [];

    public void Slowed(Mob mob, bool on)
    {
        if (on) slowed.Add(mob); else slowed.Remove(mob);
    }

    public void ReleaseHolds(TimeSpan now)
    {
        if (slowed.Count == 0) return;
        Mob[] list = [.. slowed.Where(mob => mob.Npc is { IsValid: true } && mob.State != MobState.Dead && mob.Listed)];
        var keep = new HashSet<Mob>(list);
        slowed.RemoveWhere(mob => !keep.Contains(mob));
        if (list.Length == 0) return;
        IReadOnlyList<NpcState> states = server.Npcs.Snapshot(list.Select(mob => mob.Npc!));
        for (int i = 0; i < list.Length; i++)
        {
            if (!states[i].Exists || states[i].IsDead) continue;
            list[i].LastPosition = states[i].Position;
            if (!LaneMarch.CaughtUp(list[i], states[i].Position)) continue;
            slowed.Remove(list[i]);
            Wake(list[i]);
        }
    }

    public void Think(TimeSpan now, IReadOnlyList<Combatant> players)
    {
        ApplyDueDamage(now);
        var living = new List<Mob>(mobs.Count);
        bool respawned = false;
        HashSet<Camp> occupied = [.. mobs.Where(m => m.Npc is not null && m.Camp is { Respawn: not null }).Select(m => m.Camp!)];
        foreach (Mob mob in mobs.ToArray())
        {
            if (mob.Npc is null)
            {
                if (!mob.Transient && now >= mob.RespawnAt && !(mob.Camp is { Respawn: not null } closed && occupied.Contains(closed)))
                {
                    Spawn(mob, now);
                    respawned |= mob.Npc is not null;
                    if (mob.Npc is not null && mob.Kind.Immobile) { TurretSpawned?.Invoke(mob); StructureRebuilt?.Invoke(mob); }
                    if (mob.Npc is not null && mob.Kind.FirstSpawn is not null && mob.Camp is not null) ObjectiveSpawned?.Invoke(mob);
                }
                continue;
            }
            if (mob.ExpiresAt is { } expires && now >= expires && !mob.DeathReported) Retire(mob, "czas minął");
            if (!mob.Npc.IsValid || mob.DeathReported) { Die(mob, now); continue; }
            living.Add(mob);
        }
        if (respawned) PopulationChanged?.Invoke();
        if (living.Count == 0) return;

        IReadOnlyList<NpcState> states = server.Npcs.Snapshot(living.Select(mob => mob.Npc!));
        var combatants = new List<Combatant>(living.Count);
        for (int i = 0; i < living.Count; i++)
        {
            NpcState state = states[i];
            if (state.Exists && !state.IsDead) combatants.Add(new Combatant(living[i].Npc!, state.Position, living[i]));
        }
        lastPlayers = players;
        lastMobs = combatants;
        var index = new Dictionary<Character, Combatant>(players.Count + combatants.Count);
        foreach (Combatant player in players) index.TryAdd(player.Character, player);
        foreach (Combatant other in combatants) index.TryAdd(other.Character, other);
        lastIndex = index;
        PruneHits(now);
        for (int i = 0; i < living.Count; i++)
        {
            Mob mob = living[i];
            NpcState state = states[i];
            if (!state.Exists || state.IsDead) { Die(mob, now); continue; }
            Run(mob, state, now);
        }
    }

    public void Wake(Mob mob)
    {
        if (!woken.Add(mob) || wakeScheduled) return;
        wakeScheduled = true;
        server.Post(_ => WakeAll());
    }

    private void WakeAll()
    {
        wakeScheduled = false;
        Mob[] batch = [.. woken];
        woken.Clear();
        foreach (Mob mob in batch) ThinkOne(mob);
    }

    private void ThinkOne(Mob mob)
    {
        if (mob.Npc is not { IsValid: true } npc || mob.State == MobState.Dead || !mob.Listed) return;
        TimeSpan now = server.Clock.Elapsed;
        NpcState state = server.Npcs.Snapshot([npc])[0];
        if (!state.Exists || state.IsDead) { Die(mob, now); return; }
        Run(mob, state, now);
    }

    private void Run(Mob mob, NpcState state, TimeSpan now)
    {
        mob.Host = state.Host;
        try { brain.Think(mob, state, now, lastPlayers, lastMobs, lastIndex); }
        catch (Exception error)
        {
            mob.Note = "błąd: " + error.Message;
            server.Log($"{mob.Label}: {error.Message}");
        }
    }

    private void FixDamage(CharacterDamagedEvent e)
    {
        Mob? attacker = e.Attacker is Npc a && byNpc.TryGetValue(a, out Mob? found) ? found : null;
        Mob? victim = e.Victim is Npc v && byNpc.TryGetValue(v, out Mob? hit) ? hit : null;
        if (TeamOf(e.Attacker) is { } attackerTeam && attackerTeam == TeamOf(e.Victim))
        {
            if (LaneSettings.LogSpells && e.Amount > 0) server.Log($"[DMG] swoi: {e.Attacker} -> {e.Victim}: {e.Amount} -> 0 (ta sama drużyna)");
            e.Amount = 0;
            return;
        }
        if (victim is not null && IsProtected(victim))
        {
            if (e.Amount > 0 && e.Attacker is Player blocked) HitBlocked?.Invoke(blocked, victim);
            e.Amount = 0;
            return;
        }
        if (attacker is not null && (attacker.Behaviour.Has<DrakeCombat>() || attacker.Behaviour.Has<BaronCombat>())) { e.Amount = 0; return; }
        if (attacker is { Camp: not null, Champion: false, Owner: null } beast && beast.Kind.Damage is not null && !beast.Kind.Immobile)
        {
            e.Amount = AttackDamage(beast, e.Victim);
            e.Amount = Shielded(victim, e.Amount, server.Clock.Elapsed);
            return;
        }
        if (attacker?.Lane is null && attacker?.IsHelper != true && victim?.Lane is null) return;
        int original = e.Amount;
        if (victim?.Kind.Immobile == true && e.Attacker is Npc) e.Amount = 0;
        else
        if (attacker is not null && (attacker.Kind.Damage is not null || attacker.Kind.Role is not null)) e.Amount = AttackDamage(attacker, e.Victim);
        else if (victim?.Lane is not null && e.Attacker is null) e.Amount = 0;
        e.Amount = victim is { Lane: null }
            ? Incoming(victim, e.Attacker, e.Amount, e.Description.SpellId is not null, server.Clock.Elapsed)
            : Shielded(victim, e.Amount, server.Clock.Elapsed);
        if (LaneSettings.LogSpells)
            server.Log($"[DMG] {attacker?.Label ?? e.Attacker?.ToString() ?? "-"} -> {victim?.Label ?? e.Victim.ToString()}: {original} -> {e.Amount}, flagi {e.Description.Flags.Value}, czar {e.Description.SpellId?.ToString() ?? "-"}");
    }

    private Faction? TeamOf(Character? who) => who switch
    {
        Player player => Teams.Of(player),
        Npc npc when byNpc.TryGetValue(npc, out Mob? mob) && mob.Kind.Faction is Faction.Blue or Faction.Red => mob.Kind.Faction,
        _ => null,
    };

    private void SetHealth(Character target, int value)
    {
        if (target is Npc npc && byNpc.TryGetValue(npc, out Mob? mob)) mob.AuthHealth = value;
        target.Health = value;
    }

    private Mob? MobOfNpcId(int id) => byId.TryGetValue(id, out Mob? mob) && mob.Npc is { IsValid: true } ? mob : null;

    private readonly Dictionary<(int Attacker, int Victim), TimeSpan> lastHit = [];
    private readonly Dictionary<int, TimeSpan> lastBlow = [];

    public void ReportDamage(Player reporter, int attackerId, int victimId, int rawDamage, TimeSpan now)
    {
        Mob? victim = MobOfNpcId(victimId);
        if (victim?.Npc is not { IsValid: true } victimNpc || victim.State == MobState.Dead || victimNpc.IsDead) return;
        if (attackerId < 0) return;
        Character? attacker;
        Mob? attackerMob = null;
        if (attackerId < server.MaxSlots)
        {
            Player? who = server.Players.Find(new PlayerId(attackerId));
            if (who is null || who.Id != reporter.Id)
            {
                if (LaneSettings.LogSpells) server.Log($"[DMG] odrzucono: {reporter.Name} zgłasza cudzy cios (napastnik {attackerId})");
                return;
            }
            attacker = who;
        }
        else
        {
            attackerMob = MobOfNpcId(attackerId);
            if (attackerMob?.Npc is not { IsValid: true } attackerNpc) return;
            if (victim.Kind.Immobile) return;
            if (victim.Lane is null) return;
            attacker = attackerNpc;
        }
        if (!attacker.IsValid || attacker.IsDead) return;
        if (TeamOf(attacker) is { } team && team == TeamOf(victimNpc)) return;
        if (IsProtected(victim))
        {
            if (attacker is Player blocked) HitBlocked?.Invoke(blocked, victim);
            return;
        }
        if (attacker.Position.DistanceTo(victimNpc.Position).Centimeters > (attackerMob is null ? LaneSettings.MaxHitDistance : LaneSettings.MaxHitDistance / 4)) return;
        float maxReach = victim.Kind.Structure == StructureKind.Turret ? LaneSettings.TowerHitRange.Centimeters : LaneSettings.MaxAttackRange.Centimeters;
        if (attackerMob is null && attacker.Position.DistanceTo(victimNpc.Position).Centimeters
            > maxReach + (victim.Kind.Immobile ? 0f : victim.Kind.Radius) + LaneSettings.AttackRangeSlack)
        {
            if (LaneSettings.LogSpells) server.Log($"[DMG] odrzucono: cios {((Player)attacker).Name} spoza zasięgu ataku ({attacker.Position.DistanceTo(victimNpc.Position).Centimeters:0} cm)");
            return;
        }
        var pair = (attackerId, victimId);
        if (lastHit.TryGetValue(pair, out TimeSpan previous) && now - previous < LaneSettings.HitGap) return;
        if (attackerMob is null && attacker is Player swinger && !AutoAttackGate.Allow(attackerId, now, out int suspicious))
        {
            if (suspicious > 0) server.Log($"[ANTYCHEAT] {swinger.Name}: {suspicious} zbyt szybkich ciosów z rzędu (odstęp < {LaneSettings.AutoAttackGap.TotalMilliseconds:0} ms) — możliwy speedhack");
            return;
        }
        if (attackerMob is not null)
        {
            if (lastBlow.TryGetValue(attackerId, out TimeSpan blow) && now - blow < attackerMob.Kind.AttackCooldown / 2) return;
            lastBlow[attackerId] = now;
        }
        lastHit[pair] = now;
        int amount = attackerMob is not null ? AttackDamage(attackerMob, victimNpc) : Math.Clamp(rawDamage, 0, LaneSettings.PlayerMaxHit);
        if (attackerMob is null && attacker is Player striker && BasicDamage is { } basic) amount = basic(striker, amount);
        if (attackerMob is null && attacker is Player hitter && CritRoll?.Invoke(hitter) == true) amount = (int)Math.Round(amount * (CritMultiplier?.Invoke(hitter) ?? BotArena.Heroes.HeroResources.CritMultiplier));
        if (attackerMob is null && attacker is Player buffed && PlayerHit is { } modify) amount = modify(buffed, victimNpc, amount);
        bool magicBlow = attackerMob is null && attacker is Player caster && BasicMagic?.Invoke(caster) == true;
        if (victim.Champion && ChampionMitigation is { } armor) amount = (int)Math.Round(amount * armor(victim, magicBlow));
        else if (victim.Kind.Immobile && attackerMob is not { Champion: false })
            amount = (int)Math.Round(amount * BotArena.Heroes.HeroResources.Mitigation(MinionStats.StructureArmor(victim.Kind.Structure)));
        amount = Incoming(victim, attacker, amount, magicBlow, now);
        if (amount <= 0) return;
        int before = victim.AuthHealth;
        int after = Math.Max(0, before - amount);
        if (after == 0) victim.Killer = attacker;
        SetHealth(victimNpc, after);
        OnDamaged(victimNpc, attacker, amount, magicBlow);
        if (LaneSettings.LogSpells)
            server.Log($"[DMG] {(attackerMob?.Label ?? ((Player)attacker).Name)} -> {victim.Label}: {amount} (gra {rawDamage}), HP {before} -> {after}");
    }

    private void OnDamaged(Character victim, Character? attacker, int amount, bool magic = false)
    {
        RecordAggression(attacker, victim, amount, magic);
        if (attacker is null || victim is not Npc npc || !byNpc.TryGetValue(npc, out Mob? mob)) return;
        if (attacker is Npc other && byNpc.TryGetValue(other, out Mob? ally)
            && ((ally.Camp is not null && ally.Camp == mob.Camp) || (ally.Lane is not null && ally.Kind.Faction == mob.Kind.Faction))) return;
        TimeSpan now = server.Clock.Elapsed;
        Position? at = attacker.IsValid ? attacker.Position : null;
        KnownCharacter known = at is { } position ? mob.Memory.See(attacker, position, now) : mob.Memory.Know(attacker);
        known.Threat += amount + 30;
        mob.Memory.StopIgnoring(attacker);
        mob.LastHurt = now;
        var hit = new MobHit(mob, attacker, at, amount, now, this);
        foreach (MobComponent component in mob.Behaviour.Components) component.Damaged(hit);
        if (mob.LastHitReaction is { } previous && now - previous < EventThinkGap) return;
        mob.LastHitReaction = now;
        Wake(mob);
    }

    private void Die(Mob mob, TimeSpan now)
    {
        Character? killer = mob.Killer;
        Npc? npc = mob.Npc;
        Despawn(mob);
        mob.State = MobState.Dead;
        mob.RespawnAt = now + mob.Kind.RespawnDelay;
        if (mob.Camp is { Respawn: { } campRespawn } camp && !mob.Summoned)
        {
            bool anyAlive = mobs.Any(m => !ReferenceEquals(m, mob) && m.Camp == camp && m.Npc is not null);
            if (!anyAlive)
                foreach (Mob mate in mobs)
                    if (mate.Camp == camp) mate.RespawnAt = now + campRespawn;
        }
        mob.Note = mob.Camp is { Respawn: not null } ? "zginął, obóz wraca naraz z resztą" : mob.Champion || mob.Kind.Respawns ? $"zginął, wróci za {mob.Kind.RespawnDelay.TotalSeconds:0} s"
            : mob.Lane is not null ? "zginął (minion — nie wróci)"
            : mob.Summoned ? "zginął (przywołany — nie wróci)" : $"zginął, wróci za {mob.Kind.RespawnDelay.TotalSeconds:0} s";
        if (mob.Transient) Unlist(mob);
        foreach (MobComponent component in mob.Behaviour.Components) component.Died(mob, killer);
        if (mob.Behaviour.Get<Pack>() is { Leader: true } && mob.Camp is not null && killer is not null) BreakMorale(mob.Camp, killer, now);
        MobKilled?.Invoke(mob, killer);
        if (mob.Lane is not null) return;
        PopulationChanged?.Invoke();
        if (npc is not null) server.Log($"{mob.Label} zginął{(killer is Player player ? $" z ręki {player.Name}" : "")}.");
    }

    private void BreakMorale(Camp camp, Character killer, TimeSpan now)
    {
        if (!killer.IsValid || killer.IsDead) return;
        foreach (Mob mate in mobs)
        {
            if (mate.Camp != camp || mate.Npc is not { IsValid: true } || mate.State == MobState.Dormant || !mate.Behaviour.Has<Flee>()) continue;
            Flee.Panic(mate, killer, now + TimeSpan.FromSeconds(5));
            Wake(mate);
        }
    }

    private void Spawn(Mob mob, TimeSpan now, Position? at = null, int? health = null)
    {
        Enlist(mob);
        MobKind kind = mob.Kind;
        Npc? npc = null;
        try
        {
            npc = server.Npcs.Create(kind.DisplayName, kind.Instance);
            npc.World = ArenaSettings.World;
            npc.VirtualWorld = world;
            npc.SetRespawnDelay(null);
            npc.MaxHealth = mob.MaxHealth;
            int startHealth = Math.Clamp(health ?? mob.MaxHealth, 1, mob.MaxHealth);
            mob.AuthHealth = startHealth;
            npc.Health = startHealth;
            npc.Strength = kind.Strength;
            npc.Dexterity = kind.Dexterity;
            if (kind.MeleeWeapon is { } melee)
            {
                npc.SetWeaponSkill(kind.MeleeMode == WeaponMode.TwoHandedSword ? WeaponSkill.TwoHanded : WeaponSkill.OneHanded, new Proficiency(kind.Skill));
                npc.Inventory.Give(melee);
                npc.Inventory.Equip(melee);
            }
            if (kind.RangedWeapon is { } ranged)
            {
                npc.SetWeaponSkill(WeaponSkill.Bow, new Proficiency(kind.Skill));
                npc.Inventory.Give(ranged);
                npc.Inventory.Equip(ranged);
                if (kind.Ammo is { } ammo) npc.Inventory.Give(ammo, 500);
            }
            if (kind.Scale != 1f) npc.Scale = new GothicOnline.Api.Scale3(kind.Scale, kind.Scale, kind.Scale);
            if (kind.Rune is { } rune)
            {
                npc.MaxMana = kind.Mana;
                npc.Mana = kind.Mana;
                npc.MagicLevel = 6;
                npc.Inventory.Give(rune);
                npc.Inventory.Equip(rune, LaneSettings.SpellSlot);
            }
            if (kind.Armor is { } armor)
            {
                npc.Inventory.Give(armor);
                npc.Inventory.Equip(armor);
            }
            if (kind.Look is { } look)
            {
                try { npc.SetVisual(look, kind.Armor); } catch (Exception) {  }
            }
            Position position = at ?? mob.Home;
            npc.Position = position;
            npc.Spawn();
            npc.VirtualWorld = world;
            if (!kind.Immobile && !kind.Humanoid && (kind.Behaviour.Has<MeleeCombat>() || (mob.Lane is not null && kind.Behaviour.Has<BoltCombat>()))) npc.WeaponMode = WeaponMode.Fists;
            if (mob.Facing is { } facing) npc.SetAngle(new Angle(facing), false);
            if (mob.StartWeaponMode is { } startMode && startMode != WeaponMode.None)
            {
                try { npc.WeaponMode = startMode; } catch (Exception) {  }
            }
            mob.Npc = npc;
            byNpc[npc] = mob;
            byId[npc.Id.Value] = mob;
            mob.ResetForSpawn(kind.Behaviour.Tree.CreateMemory(), position, startHealth);
            mob.Memory.Get<CombatState>().NextAttack = now;
        }
        catch (Exception error)
        {
            if (npc?.IsValid == true) npc.Destroy();
            mob.Npc = null;
            mob.State = MobState.Dead;
            mob.RespawnAt = now + TimeSpan.FromSeconds(10);
            server.Log($"{mob.Label}: nie udało się stworzyć NPC ({error.Message}); ponowna próba za 10 s.");
        }
    }

    private void Enlist(Mob mob)
    {
        if (mob.Listed) return;
        mob.Listed = true;
        mobs.Add(mob);
        if (mob.Kind.Immobile) structures.Add(mob);
    }

    private void Unlist(Mob mob)
    {
        if (!mob.Listed) return;
        mob.Listed = false;
        mobs.Remove(mob);
        if (mob.Kind.Immobile) structures.Remove(mob);
    }

    private void PruneHits(TimeSpan now)
    {
        if (now < nextPrune) return;
        nextPrune = now + TimeSpan.FromSeconds(10);
        TimeSpan stale = TimeSpan.FromSeconds(10);
        foreach (var key in lastHit.Where(pair => now - pair.Value > stale).Select(pair => pair.Key).ToArray()) lastHit.Remove(key);
        foreach (int key in lastBlow.Where(pair => now - pair.Value > stale).Select(pair => pair.Key).ToArray()) lastBlow.Remove(key);
        foreach (PlayerId key in lastPlayerAttacker.Where(pair => now - pair.Value.At > stale).Select(pair => pair.Key).ToArray()) lastPlayerAttacker.Remove(key);
        foreach (var entry in assistLedger.ToArray())
        {
            foreach (var hit in entry.Value.Where(pair => now - pair.Value.At > stale).ToArray()) entry.Value.Remove(hit.Key);
            if (entry.Value.Count == 0) assistLedger.Remove(entry.Key);
        }
    }

    private void Despawn(Mob mob)
    {
        if (mob.Npc is not { } npc) return;
        byNpc.Remove(npc);
        byId.Remove(npc.Id.Value);
        mob.Npc = null;
        mob.Host = null;
        if (mob.Kind.Immobile) TurretRemoved?.Invoke(npc.Id.Value);
        if (npc.IsValid) npc.Destroy();
    }
}
