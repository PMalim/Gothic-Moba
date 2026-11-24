using BotArena.GameData;
using BotArena.Matches;
using GothicOnline.Api;

namespace BotArena.Heroes;

public sealed partial class AbilityService(Server server, MatchService matches)
{
    private const float ProjectileSpeed = 1800f;
    private const float ConeCos = 0.55f;

    private sealed record Pending(TimeSpan Due, Character? Target, int Amount, Player? Attacker, bool Unfreeze = false, bool Magic = false);

    private readonly Dictionary<PlayerId, TimeSpan[]> ready = [];
    private readonly Dictionary<PlayerId, (TimeSpan Until, float Reduction)> shields = [];
    private readonly Dictionary<PlayerId, (PlayerId Attacker, TimeSpan At)> lastHit = [];
    private readonly List<Pending> pending = [];

    public event Action<Player, int, int>? Cooldown;
    public event Action<Player, string>? Notice;
    public event Action<Player, int, int, float>? ManaChanged;

    public event Action<Player, object>? BuffsChanged;

    private readonly Dictionary<PlayerId, BuffState> buffs = [];
    private readonly List<CurseZone> zones = [];
    private readonly Dictionary<PlayerId, double> mana = [];
    private TimeSpan nextManaPush;

    public void Start()
    {
        server.Network.Register<AbilityCastMessage>();
        server.Network.Register<AbilityFxMessage>();
        server.Network.Register<AbilityDashMessage>();
        server.Network.Register<AbilityAuraMessage>();
        server.Network.Register<AbilityGroundMessage>();
        server.Network.Register<AbilitySpeedMessage>();
        server.Network.Register<AbilityRollMessage>();
        server.Network.Register<AbilityAnimMessage>();
        server.Network.Register<AbilityAimMessage>();
        server.Network.Register<AbilityThrowMessage>();
        server.Network.Register<AbilityBarrelMessage>();
        server.Network.Register<AbilityMotionMessage>();
        server.Network.Register<ArenaStasisMessage>();
        server.Network.Register<AbilityRankMessage>();
        server.Network.Register<AbilitySwordMessage>();
        server.Network.Register<AbilityTotemMessage>();
        server.Network.Register<AbilitySkullMessage>();
        matches.BotKilled += TribeMobKill;
        matches.ChampionSlain += TribeChampionKill;
        matches.ChampionSlain += Servant;
        server.Network.On<AbilityRankMessage>(RankMessage);
        server.Network.On<AbilityCastMessage>(Cast);
        server.Network.On<AbilityThrowMessage>(Throw);
        server.Events.CharacterDamaged.Subscribe(GuardPlayerHit);
        server.Events.CharacterDamaged.Subscribe(DefendPlayer, priority: DefendPriority);
        server.Timers.Every(TimeSpan.FromMilliseconds(100), Update);
    }

    private const int DefendPriority = 10000;

    public float ShieldFactor(Player player) =>
        shields.TryGetValue(player.Id, out var shield) && server.Clock.Elapsed < shield.Until ? 1f - shield.Reduction : 1f;

    private readonly Dictionary<(int Attacker, PlayerId Victim), TimeSpan> lastBlow = [];

    private void GuardPlayerHit(CharacterDamagedEvent e)
    {
        if (e.Victim is not Player victim) return;
        TimeSpan now = server.Clock.Elapsed;
        if (e.Amount > 0) { Touch(victim, now); CancelRecall(victim, "Powrót do bazy przerwany."); }
        if (e.Attacker is not { } attacker || ReferenceEquals(attacker, victim)) return;
        float distance = attacker.Position.DistanceTo(victim.Position).Centimeters;
        if (attacker is Player hitter)
        {
            if (matches.Of(hitter) is not { } match || !ReferenceEquals(match, matches.Of(victim))) { Reject(e, hitter.Name, victim, "inna gra"); return; }
            if (Teams.Of(hitter) == Teams.Of(victim)) { Reject(e, hitter.Name, victim, "ta sama drużyna"); return; }
            if (distance > LaneSettings.MaxAttackRange.Centimeters + LaneSettings.AttackRangeSlack) { Reject(e, hitter.Name, victim, $"poza zasięgiem ({distance:0} cm)"); return; }
            var pair = (hitter.Id.Value, victim.Id);
            if (lastBlow.TryGetValue(pair, out TimeSpan previous) && now - previous < LaneSettings.HitGap) { Reject(e, hitter.Name, victim, "ten sam cios"); return; }
            if (e.Description.SpellId is null && !AutoAttackGate.Allow(hitter.Id.Value, now, out int suspicious))
            {
                if (suspicious > 0) server.Log($"[ANTYCHEAT] {hitter.Name}: {suspicious} zbyt szybkich ciosów z rzędu (odstęp < {LaneSettings.AutoAttackGap.TotalMilliseconds:0} ms) — możliwy speedhack");
                Reject(e, hitter.Name, victim, "za szybki auto atak");
                return;
            }
            lastBlow[pair] = now;
            bool magicBlow = BasicIsMagic(hitter);
            int amount = BasicDamage(hitter, Math.Clamp(e.Amount, 0, LaneSettings.PlayerMaxHit));
            if (StatsOf(hitter) is { } stats && Random.Shared.Next(100) < stats.Crit) amount = (int)Math.Round(amount * CritFactor(hitter));
            e.Amount = ModifyPlayerHit(hitter, victim, amount);
            Defend(e, victim, magicBlow, now);
            return;
        }
        float reach = LaneSettings.MaxAttackRange.Centimeters + LaneSettings.AttackRangeSlack + (matches.Of(victim)?.Director?.MobOf(attacker)?.Kind.Radius ?? 0f);
        if (distance > reach) Reject(e, attacker.ToString() ?? "NPC", victim, $"NPC poza zasięgiem ({distance:0} cm)");
    }

    private void DefendPlayer(CharacterDamagedEvent e)
    {
        if (e.Victim is not Player victim || (e.Attacker is Player hitter && !ReferenceEquals(hitter, victim))) return;
        Defend(e, victim, magic: e.Attacker is not null && e.Description.SpellId is not null, server.Clock.Elapsed);
    }

    private void Defend(CharacterDamagedEvent e, Player victim, bool magic, TimeSpan now)
    {
        if (StatsOf(victim) is { } defence) e.Amount = (int)Math.Round(e.Amount * HeroResources.Mitigation(Penetrated(magic ? defence.Resist : defence.Armor, e.Attacker, magic)));
        if (shields.TryGetValue(victim.Id, out var shield) && now < shield.Until)
            e.Amount = (int)Math.Round(e.Amount * (1f - shield.Reduction));
    }

    private void Reject(CharacterDamagedEvent e, string attacker, Player victim, string why)
    {
        e.Cancel();
        if (LaneSettings.LogSpells) server.Log($"[DMG] odrzucono cios {attacker} -> {victim.Name}: {why}");
    }

    public Player? RecentAttacker(PlayerId victim) =>
        lastHit.TryGetValue(victim, out var hit) && server.Clock.Elapsed - hit.At < TimeSpan.FromSeconds(10) ? server.Players.Find(hit.Attacker) : null;

    public void Reset(Player player)
    {
        ready.Remove(player.Id);
        CancelRecall(player);
        shields.Remove(player.Id);
        if (stasis.Remove(player.Id) && matches.Of(player) is { } stasisMatch) matches.Send(stasisMatch, new ArenaStasisMessage(MobCommands.IdOf(player), false));
        EndBuffs(player);
        zones.RemoveAll(zone => zone.Caster.Id == player.Id);
        ResetUrshak(player);
        moving.Remove(player.Id);
        lastSample.Remove(player.Id);
        matches.Of(player)?.Director?.RetireOwned(player);
        PushBuffs(player);
        if (HeroOf(player) is { } hero)
        {
            mana[player.Id] = MaxManaOf(player, hero, LevelOf(player));
            PushMana(player, hero);
        }
        else mana.Remove(player.Id);
    }

    public int BasicDamage(Player player, int gameDamage) => BasicOf(player) is { } info ? info.Damage : gameDamage;

    public bool BasicIsMagic(Player player) => BasicOf(player)?.Magic == true;

    public BasicAttackInfo? BasicOf(Player player) =>
        HeroOf(player) is { } hero && StatsOf(player) is { } stats ? HeroResources.BasicAttackOf(hero, stats) : null;

    public float CritFactor(Player player) => HeroResources.CritMultiplier + ItemsOf(player).CritDmg / 100f;

    public HeroStats? StatsOf(Player player)
    {
        if (HeroOf(player) is not { } hero || matches.Of(player)?.Players.GetValueOrDefault(player.Id) is not { } mp) return null;
        HeroStats b = HeroResources.StatsOf(hero, mp.Level);
        Shop.ItemStats it = Shop.ItemCatalog.Bonus(mp);
        int drakes = matches.Of(player)?.DrakeStacks.GetValueOrDefault(mp.Team) ?? 0;
        b = b with { Ad = b.Ad + drakes * DrakeSettings.BuffAd, Ap = b.Ap + drakes * DrakeSettings.BuffAp };
        return new HeroStats(b.Ad + it.Ad, b.Ap + it.Ap, Math.Min(100, b.Crit + it.Crit), b.Armor + it.Armor, b.Resist + it.Resist, Math.Min(HeroResources.MaxCdr, b.Cdr + it.Cdr), b.Regen + it.HpRegen);
    }

    private Shop.ItemStats ItemsOf(Player player) => matches.Of(player)?.Players.GetValueOrDefault(player.Id) is { } mp ? Shop.ItemCatalog.Bonus(mp) : default;

    private int Penetrated(int defence, Character? attacker, bool magic)
    {
        if (attacker is not Player hitter || defence <= 0) return defence;
        Shop.ItemStats it = ItemsOf(hitter);
        int percent = Math.Clamp(magic ? it.MagPenPct : it.ArmPen, 0, 100);
        int flat = magic ? it.MagPen : it.Leth;
        return Math.Max(0, (int)Math.Round(defence * (100 - percent) / 100.0) - flat);
    }

    private void LeechHeal(Player attacker, int damage, bool basicHit)
    {
        if (damage <= 0 || !attacker.IsValid || attacker.IsDead) return;
        Shop.ItemStats it = ItemsOf(attacker);
        int percent = it.Ov + (basicHit ? it.Ls : 0);
        if (percent <= 0) return;
        int heal = (int)Math.Round(damage * percent / 100.0);
        if (heal > 0) attacker.Health = Math.Min(attacker.MaxHealth, attacker.Health + heal);
    }

    public bool GiveMana(Player player, int amount)
    {
        if (matches.Of(player)?.Players.GetValueOrDefault(player.Id) is not { } mp || HeroCatalog.Find(mp.Hero) is not { } hero || !mana.TryGetValue(player.Id, out double current)) return false;
        mana[player.Id] = Math.Min(MaxManaOf(player, hero, LevelOf(player)), current + amount);
        PushMana(player, hero);
        return true;
    }

    private int MaxManaOf(Player player, HeroDef hero, int level) => HeroResources.MaxMana(hero, level) + ItemsOf(player).Mana;

    public void ApplyItems(Player player, Shop.ItemStats before, Shop.ItemStats after)
    {
        if (!player.IsValid || HeroOf(player) is not { } hero) return;
        int dHealth = after.Health - before.Health;
        if (dHealth != 0)
        {
            if (buffs.GetValueOrDefault(player.Id) is { RampageBaseHealth: > 0 } rampage) rampage.RampageBaseHealth += dHealth;
            player.MaxHealth = Math.Max(1, player.MaxHealth + dHealth);
            if (!player.IsDead) player.Health = Math.Clamp(player.Health + Math.Max(0, dHealth), 1, player.MaxHealth);
        }
        int dMana = after.Mana - before.Mana;
        if (dMana != 0 && mana.TryGetValue(player.Id, out double current))
        {
            mana[player.Id] = Math.Clamp(current + Math.Max(0, dMana), 0, MaxManaOf(player, hero, LevelOf(player)));
            PushMana(player, hero);
        }
    }

    private int LevelOf(Player player) => matches.Of(player)?.Players.GetValueOrDefault(player.Id)?.Level ?? 1;

    public void ApplyLevel(Player player)
    {
        if (!player.IsValid || HeroOf(player) is not { } hero) return;
        int level = LevelOf(player);
        BuffState? state = buffs.GetValueOrDefault(player.Id);
        bool rampage = state is not null && state.RampageBaseHealth > 0;
        int newBase = HeroResources.MaxHealth(hero, level) + ItemsOf(player).Health;
        int oldBase = rampage ? state!.RampageBaseHealth : player.MaxHealth;
        if (newBase != oldBase)
        {
            int delta = newBase - oldBase;
            if (rampage) state!.RampageBaseHealth = newBase;
            player.MaxHealth = Math.Max(1, player.MaxHealth + delta);
            if (!player.IsDead) player.Health = Math.Clamp(player.Health + Math.Max(0, delta), 1, player.MaxHealth);
        }
        double newMana = MaxManaOf(player, hero, level);
        if (mana.TryGetValue(player.Id, out double current))
        {
            double oldMana = MaxManaOf(player, hero, level - 1);
            mana[player.Id] = Math.Min(newMana, current + Math.Max(0, newMana - oldMana));
            PushMana(player, hero);
        }
        if (matches.Of(player)?.Players.GetValueOrDefault(player.Id) is { } leveled && HeroResources.SkillPoints(level, leveled.Ranks) > 0)
            Notice?.Invoke(player, $"Poziom {level}! Masz punkt umiejętności: Shift + Z / X / C / V.");
    }

    private HeroDef? HeroOf(Player player) =>
        matches.Of(player) is { } match && match.Players.TryGetValue(player.Id, out MatchPlayer? mp) ? HeroCatalog.Find(mp.Hero) : null;

    private AbilityDef? PassiveOf(Player player, AbilityKind kind)
    {
        if (matches.Of(player) is not { } match || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp) || HeroCatalog.Find(mp.Hero) is not { } hero) return null;
        int slot = Array.FindIndex(hero.Abilities, a => a.Kind == kind);
        if (slot < 0 || slot >= mp.Ranks.Length || mp.Ranks[slot] == 0) return null;
        return HeroResources.AtRank(hero.Abilities[slot], mp.Ranks[slot]);
    }

    private void PushMana(Player player, HeroDef hero) =>
        ManaChanged?.Invoke(player, (int)Math.Round(mana.GetValueOrDefault(player.Id)), MaxManaOf(player, hero, LevelOf(player)),
            HeroResources.ManaRegen(hero, LevelOf(player)) * (float)(1 + ItemsOf(player).ManaRegen / 100.0));

    private void Cast(Player player, AbilityCastMessage message)
    {
        if (player.IsDead || matches.Of(player) is not { State: MatchState.Running, Director: { } director } match
            || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return;
        if (message.Slot == RecallSlot) { StartRecall(player); return; }
        if (HeroCatalog.Find(mp.Hero) is not { } hero || message.Slot < 0 || message.Slot >= hero.Abilities.Length) return;
        AbilityDef baseAbility = hero.Abilities[message.Slot];
        AbilityDef ability = baseAbility;
        if (InStasis(player)) { Notice?.Invoke(player, "W stasis nic nie zrobisz."); return; }
        int rank = mp.Ranks[message.Slot];
        if (ability.IsPassive)
        {
            if (rank > 0) Notice?.Invoke(player, $"{ability.Name} działa sama (pasywna, stopień {rank}); Shift + klawisz ją ulepsza.");
            else if (!RankUp(player, message.Slot)) Notice?.Invoke(player, $"{ability.Name}: pasywna działa dopiero po nauczeniu (punkt umiejętności, Shift + klawisz).");
            return;
        }
        if (rank == 0)
        {
            if (!RankUp(player, message.Slot)) Notice?.Invoke(player, $"{ability.Name}: najpierw ulepsz zdolność punktem umiejętności (Shift + klawisz).");
            return;
        }
        ability = HeroResources.AtRank(baseAbility, rank);
        TimeSpan now = server.Clock.Elapsed;
        TimeSpan[] cooldowns = ready.TryGetValue(player.Id, out TimeSpan[]? known) ? known : (ready[player.Id] = new TimeSpan[4]);
        if (now < cooldowns[message.Slot]) return;
        int cost = HeroResources.ManaCost(baseAbility, rank);
        if (mana.GetValueOrDefault(player.Id) < cost) { Notice?.Invoke(player, "Za mało many."); return; }

        float length = MathF.Sqrt(message.DirX * message.DirX + message.DirZ * message.DirZ);
        if (length < 0.001f || !float.IsFinite(length)) return;
        (float dx, float dz) = (message.DirX / length, message.DirZ / length);
        Position at = player.Position;
        Faction team = mp.Team;
        if (ability.Kind == AbilityKind.Barrel)
        {
            server.Network.Send(player, new AbilityAimMessage(message.Slot, Reach(ability), ability.Radius, BarrelVisual));
            return;
        }
        if (ability.Kind == AbilityKind.BeliarMight)
        {
            server.Network.Send(player, new AbilityAimMessage(message.Slot, ability.Range, ability.Radius, SwordVisual));
            return;
        }

        bool cast = ability.Kind switch
        {
            AbilityKind.Strike => Strike(match, director, player, ability, team, at, dx, dz, now),
            AbilityKind.Nova => Nova(match, director, player, ability, team, at, now),
            AbilityKind.Rain => Rain(match, director, player, ability, team, at, dx, dz, now),
            AbilityKind.Dash => Dash(match, director, player, ability, team, at, dx, dz, now),
            AbilityKind.Heal => Heal(match, player, ability, team, at),
            AbilityKind.Shield => Shield(match, player, ability, now),
            AbilityKind.Empower => Empower(match, player, ability, now),
            AbilityKind.Haste => Haste(match, player, ability, now),
            AbilityKind.Curse => Curse(match, player, ability, team, at, dx, dz, now),
            AbilityKind.Rampage => Rampage(match, player, ability, now),
            AbilityKind.Roll => Roll(match, player, ability, at, dx, dz, now),
            AbilityKind.PierceShot => PierceShot(match, player, ability, now),
            AbilityKind.HunterMode => HunterMode(match, player, ability, now),
            AbilityKind.Summon => Summon(match, director, player, hero, ability, team, at, dx, dz, now),
            AbilityKind.Decoy => Decoy(match, director, player, hero, ability, team, at, now),
            AbilityKind.Ironhead => Ironhead(match, player, ability, now),
            AbilityKind.ShadowForm => ShadowForm(match, player, hero, ability, now),
            AbilityKind.Spin => Spin(match, director, player, ability, team, at, now),
            AbilityKind.Bloodlust => Bloodlust(match, player, ability, now),
            AbilityKind.Pull => Pull(match, director, player, ability, team, at, dx, dz, now),
            AbilityKind.Execute => Execute(match, director, player, hero, ability, message.Slot, cost, team, at, now),
            AbilityKind.BeliarTongue => BeliarTongue(match, player, ability, team, at, dx, dz, now),
            AbilityKind.Flank => Flank(match, director, player, ability, team, at, now),
            AbilityKind.Ignite => Ignite(match, player, ability, now),
            AbilityKind.TribeProgress => TribeProgress(match, director, player, ability, team, now),
            AbilityKind.Totem => Totem(match, player, ability, team, at, dx, dz, now),
            AbilityKind.TribeGuard => TribeGuard(match, director, player, ability, team, at, now),
            _ => false,
        };
        if (!cast) return;
        Charge(player, hero, ability, message.Slot, cost, cooldowns, now);
    }

    private void Charge(Player player, HeroDef hero, AbilityDef ability, int slot, int cost, TimeSpan[] cooldowns, TimeSpan now)
    {
        mana[player.Id] = Math.Max(0, mana.GetValueOrDefault(player.Id) - cost);
        PushMana(player, hero);
        float seconds = HeroResources.Cooldown(ability.Cooldown, StatsOf(player)?.Cdr ?? 0);
        cooldowns[slot] = now + TimeSpan.FromSeconds(seconds);
        Cooldown?.Invoke(player, slot, (int)(seconds * 1000));
    }

    public const string BarrelVisual = "OC_BARREL_V1.3DS";
    private const float BarrelSpeed = 800f;

    private sealed record Landing(TimeSpan Due, Match Match, Player Caster, Faction Team, Position At, float Radius, float Stun, string HoldFx = "");
    private readonly List<Landing> landings = [];

    private void Throw(Player player, AbilityThrowMessage message)
    {
        if (player.IsDead || matches.Of(player) is not { State: MatchState.Running, Director: not null } match
            || !match.Players.TryGetValue(player.Id, out MatchPlayer? mp)) return;
        if (HeroCatalog.Find(mp.Hero) is not { } hero || message.Slot < 0 || message.Slot >= hero.Abilities.Length) return;
        AbilityDef baseAbility = hero.Abilities[message.Slot];
        if (baseAbility.Kind is not (AbilityKind.Barrel or AbilityKind.BeliarMight) || mp.Ranks[message.Slot] == 0) return;
        int rank = mp.Ranks[message.Slot];
        AbilityDef ability = HeroResources.AtRank(baseAbility, rank);
        TimeSpan now = server.Clock.Elapsed;
        TimeSpan[] cooldowns = ready.TryGetValue(player.Id, out TimeSpan[]? known) ? known : (ready[player.Id] = new TimeSpan[4]);
        if (now < cooldowns[message.Slot]) return;
        int cost = HeroResources.ManaCost(baseAbility, rank);
        if (mana.GetValueOrDefault(player.Id) < cost) { Notice?.Invoke(player, "Za mało many."); return; }
        if (!float.IsFinite(message.At.X) || !float.IsFinite(message.At.Y) || !float.IsFinite(message.At.Z)) return;

        Position at = player.Position;
        float dx = message.At.X - at.X, dz = message.At.Z - at.Z, flat = MathF.Sqrt(dx * dx + dz * dz), reach = ability.Kind == AbilityKind.BeliarMight ? ability.Range : Reach(ability);
        if (flat > reach) { dx *= reach / flat; dz *= reach / flat; flat = reach; }
        var to = new Position(at.X + dx, message.At.Y, at.Z + dz);
        if (ability.Kind == AbilityKind.BeliarMight)
        {
            if (!BeliarMight(match, player, ability, mp.Team, to, now)) return;
            Charge(player, hero, ability, message.Slot, cost, cooldowns, now);
            return;
        }
        var from = new Position(at.X, at.Y + 120f, at.Z);
        int millis = (int)Math.Clamp(flat / BarrelSpeed * 1000f, 450f, 1300f);
        matches.Send(match, new AbilityBarrelMessage(MobCommands.IdOf(player), from, to, millis, ability.Fx));
        landings.Add(new Landing(now + TimeSpan.FromMilliseconds(millis), match, player, mp.Team, to, ability.Radius, ability.Seconds));
        Touch(player, now);
        Charge(player, hero, ability, message.Slot, cost, cooldowns, now);
    }

    private void Land(Landing landing, TimeSpan now)
    {
        if (landing.Match.Director is not { } director || landing.Match.State != MatchState.Running) return;
        foreach (Target t in Enemies(landing.Match, director, landing.Team).ToArray())
        {
            if (t.Mob is { Kind.Immobile: true }) continue;
            if (Flat(landing.At, t.At) > landing.Radius) continue;
            if (t.Character is Player shielded && (IsImmune(shielded) || InStasis(shielded))) continue;
            Stun(director, t, now + TimeSpan.FromSeconds(landing.Stun));
            if (landing.HoldFx.Length > 0) matches.Send(landing.Match, new AbilityAuraMessage(MobCommands.IdOf(t.Character), landing.HoldFx, (int)(landing.Stun * 1000)));
        }
    }

    private bool Ironhead(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        state.IronUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.IronFx = ability.Fx;
        pending.RemoveAll(p => p.Unfreeze && p.Target is Player victim && victim.Id == player.Id);
        server.Network.Send(player, new ArenaFreezeMessage(false));
        Aura(match, player, ability.Fx, ability.Seconds);
        PushBuffs(player);
        return true;
    }

    public bool IsImmune(Player player) => buffs.TryGetValue(player.Id, out BuffState? state) && server.Clock.Elapsed < state.IronUntil;

    private bool ShadowForm(Match match, Player player, HeroDef hero, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        if (state.Shadowed(now)) return false;
        float ratio = player.MaxHealth > 0 ? player.Health / (float)player.MaxHealth : 1f;
        NpcInstance own = player.Instance;
        try { player.Instance = GameNpcs.SHADOWBEAST; }
        catch (Exception error)
        {
            server.Log($"Cieniostwór: {error.Message}");
            Notice?.Invoke(player, "Przemiana się nie udała.");
            return false;
        }
        state.ShadowOwnInstance = own;
        state.ShadowUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.ShadowBonus = ability.Heal / 100f;
        int levelMax = HeroResources.MaxHealth(hero, LevelOf(player)) + ItemsOf(player).Health;
        player.MaxHealth = levelMax;
        player.Health = Math.Max(1, (int)Math.Round(levelMax * ratio));
        server.Network.Send(player, new AbilitySpeedMessage(1f + ability.Damage / 100f, (int)(ability.Seconds * 1000)));
        Fx(match, player, null, 1, ability.Fx);
        PushBuffs(player);
        return true;
    }

    private void EndShadow(Player player, BuffState state)
    {
        NpcInstance? own = state.ShadowOwnInstance;
        state.ShadowOwnInstance = null;
        state.ShadowUntil = default;
        if (own is null || !player.IsValid) return;
        float ratio = player.MaxHealth > 0 ? player.Health / (float)player.MaxHealth : 1f;
        bool dead = player.IsDead;
        try
        {
            player.Instance = own.Value;
            if (matches.Of(player) is { } match && match.Players.TryGetValue(player.Id, out MatchPlayer? mp) && HeroCatalog.Find(mp.Hero) is { } hero)
            {
                BotArena.Matches.HeroSetup.Apply(player, hero, mp);
                if (!dead) player.Health = Math.Max(1, (int)Math.Round(player.MaxHealth * ratio));
            }
            server.Network.Send(player, new AbilitySpeedMessage(1f, 0));
        }
        catch (Exception error) { server.Log($"Koniec cieniostwora: {error.Message}"); }
    }

    private void UpdateBrewers(TimeSpan now)
    {
        foreach (PlayerId id in mana.Keys.ToArray())
        {
            if (server.Players.Find(id) is not { IsValid: true, IsDead: false } player) continue;
            if (PassiveOf(player, AbilityKind.Brewer) is not { } brew) continue;
            BuffState state = Buffs(player);
            if (state.NextBeer == default) state.NextBeer = now + BeerInterval(brew);
            if (now < state.NextBeer || now - state.LastCombat < BeerQuiet) continue;
            if (pending.Any(p => p.Unfreeze && p.Target is Player held && held.Id == player.Id)) continue;
            state.NextBeer = now + BeerInterval(brew);
            try
            {
                server.Network.Send(player, new ArenaFreezeMessage(true));
                pending.Add(new Pending(now + TimeSpan.FromSeconds(BeerStopSeconds + BeerDrinkSeconds), player, 0, null, Unfreeze: true));
                int heal = (int)Math.Round(player.MaxHealth * brew.Heal / 100f);
                After(BeerStopSeconds, () =>
                {
                    if (!player.IsValid || player.IsDead) return;
                    try
                    {
                        int before = player.Health;
                        player.Inventory.Give(GameItems.ITFO_BEER, 1);
                        player.Inventory.Use(GameItems.ITFO_BEER);
                        player.Health = Math.Min(player.MaxHealth, Math.Max(before, player.Health) + heal);
                    }
                    catch (Exception error) { server.Log($"Piwosz: {error.Message}"); }
                });
            }
            catch (Exception error) { server.Log($"Piwosz: {error.Message}"); }
        }
    }

    private const double BeerStopSeconds = 0.5, BeerDrinkSeconds = 3.0;

    private static readonly TimeSpan BeerQuiet = TimeSpan.FromSeconds(5);

    private static TimeSpan BeerInterval(AbilityDef brew) => TimeSpan.FromSeconds(brew.Seconds + Random.Shared.NextDouble() * brew.Radius);

    private void Touch(Player player, TimeSpan now) => Buffs(player).LastCombat = now;

    private sealed record Drag(Character Who, Mob? Mob, Position From, Position To, TimeSpan Start, TimeSpan End);
    private readonly List<Drag> drags = [];
    private sealed record Check(TimeSpan Due, Player Caster, Character Victim, Mob? Mob, int Slot, int Cost, bool Hero);
    private readonly List<Check> checks = [];

    private static bool WeaponDrawn(Player player) => player.WeaponMode is WeaponMode.OneHandedSword or WeaponMode.TwoHandedSword or WeaponMode.Dagger;

    private bool Spin(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, TimeSpan now)
    {
        if (!WeaponDrawn(player)) { Notice?.Invoke(player, "Obrót wymaga dobytej broni."); return false; }
        server.Network.Send(player, new AbilityMotionMessage(HeroBuffs.SpinAnimation, (int)(ability.Seconds * 1000), 0, at));
        if (ability.Fx.Length > 0) Aura(match, player, ability.Fx, ability.Seconds + 0.2f);
        spinHits.Add(new SpinHit(now + TimeSpan.FromMilliseconds(640), player, team, director, match, ability));
        Touch(player, now);
        return true;
    }

    private sealed record SpinHit(TimeSpan Due, Player Caster, Faction Team, MobDirector Director, Match Match, AbilityDef Ability);
    private readonly List<SpinHit> spinHits = [];

    private void SweepSpin(SpinHit hit, TimeSpan now)
    {
        if (!hit.Caster.IsValid || hit.Caster.IsDead) return;
        Position at = hit.Caster.Position;
        float bladeFrom = hit.Ability.Radius * HeroBuffs.SpinBladeFrom;
        int bladeHits = 0;
        foreach (Target t in Enemies(hit.Match, hit.Director, hit.Team).Where(t => t.Mob is not { Kind.Immobile: true } && Distance(at, t.At) <= hit.Ability.Radius).ToArray())
        {
            bool blade = Distance(at, t.At) >= bladeFrom;
            Hurt(hit.Director, hit.Caster, t, hit.Ability, now, blade ? 1f : HeroBuffs.SpinShaftDamage);
            if (blade && HeroOf(hit.Caster) is { Bleeds: true }) ApplyBleed(hit.Caster, t.Character, hit.Director, hit.Match);
            if (blade && (t.Mob is null || t.Mob.Champion || t.Mob.Lane is null)) bladeHits++;
        }
        int missing = hit.Caster.MaxHealth - hit.Caster.Health;
        int heal = (int)Math.Round(missing * HeroBuffs.SpinHeal[Math.Min(bladeHits, HeroBuffs.SpinHeal.Length - 1)]);
        if (heal <= 0) return;
        hit.Caster.Health = Math.Min(hit.Caster.MaxHealth, hit.Caster.Health + heal);
        Notice?.Invoke(hit.Caster, $"Obrót: ostrze trafia {bladeHits} — leczysz {heal} HP.");
    }

    private bool Bloodlust(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        state.BloodUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.BloodDrain = DamageOf(player, ability);
        state.BloodFx = ability.Fx;
        Aura(match, player, ability.Fx, ability.Seconds);
        PushBuffs(player);
        return true;
    }

    private bool Pull(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        server.Network.Send(player, new AbilityMotionMessage(HeroBuffs.PullAnimation, (int)(ability.Seconds * 1000) + 100, 2, at));
        int index = 0;
        foreach (Target t in Enemies(match, director, team).Where(t => t.Mob is not { Kind.Immobile: true } && Distance(at, t.At) <= ability.Radius).ToArray())
        {
            float side = ((index % 5) - 2) * 90f;
            var to = new Position(at.X + dx * 170f - dz * side, at.Y, at.Z + dz * 170f + dx * side);
            index++;
            drags.Add(new Drag(t.Character, t.Mob, t.At, to, now, now + TimeSpan.FromSeconds(ability.Seconds)));
        }
        Touch(player, now);
        return true;
    }

    private void UpdateDrags(TimeSpan now)
    {
        for (int i = drags.Count - 1; i >= 0; i--)
        {
            Drag drag = drags[i];
            if (!drag.Who.IsValid || drag.Who.IsDead || (drag.Who is Player dragged && InStasis(dragged)) || drag.Mob?.Behaviour.Has<BaronCombat>() == true) { drags.RemoveAt(i); continue; }
            float f = drag.End <= drag.Start ? 1f : (float)Math.Clamp((now - drag.Start).TotalSeconds / (drag.End - drag.Start).TotalSeconds, 0, 1);
            var at = new Position(drag.From.X + (drag.To.X - drag.From.X) * f, drag.From.Y + (drag.To.Y - drag.From.Y) * f, drag.From.Z + (drag.To.Z - drag.From.Z) * f);
            try
            {
                drag.Who.Position = at;
                if (drag.Mob is not null) drag.Mob.LastPosition = at;
            }
            catch (Exception) { drags.RemoveAt(i); continue; }
            if (f >= 1f) drags.RemoveAt(i);
        }
    }

    private bool Execute(Match match, MobDirector director, Player player, HeroDef hero, AbilityDef ability, int slot, int cost, Faction team, Position at, TimeSpan now)
    {
        Target? target = Enemies(match, director, team)
            .Where(t => t.Mob is not { Kind.Immobile: true } && Distance(at, t.At) <= Reach(ability))
            .OrderBy(t => t.Mob is null || t.Mob.Champion ? 0 : 1).ThenBy(t => Distance(at, t.At)).Cast<Target?>().FirstOrDefault();
        if (target is not { } victim) { Notice?.Invoke(player, "Brak celu w zasięgu."); return false; }
        float dx = victim.At.X - at.X, dz = victim.At.Z - at.Z, flat = MathF.Max(1f, MathF.Sqrt(dx * dx + dz * dz));
        var land = new Position(victim.At.X - dx / flat * 120f, victim.At.Y, victim.At.Z - dz / flat * 120f);
        int millis = (int)(ability.Seconds * 1000);
        server.Network.Send(player, new AbilityMotionMessage(HeroBuffs.ExecuteAnimation, millis, 1, land));
        Aura(match, player, ability.Fx, ability.Seconds + 0.6f);
        TimeSpan due = now + TimeSpan.FromMilliseconds(millis * HeroBuffs.ExecuteImpact);
        Hurt(director, player, victim, ability, due);
        if (hero.Bleeds) ApplyBleed(player, victim.Character, director, match);
        bool isHero = victim.Mob is null || victim.Mob.Champion;
        checks.Add(new Check(due + TimeSpan.FromMilliseconds(250), player, victim.Character, victim.Mob, slot, cost, isHero));
        Touch(player, now);
        return true;
    }

    private void UpdateChecks(TimeSpan now)
    {
        for (int i = checks.Count - 1; i >= 0; i--)
        {
            Check check = checks[i];
            if (check.Due > now) continue;
            checks.RemoveAt(i);
            bool dead = !check.Victim.IsValid || check.Victim.IsDead || (check.Mob is { } mob && (mob.Npc is null || mob.AuthHealth <= 0 || mob.State == MobState.Dead));
            if (!dead || !check.Hero || !check.Caster.IsValid) continue;
            if (ready.TryGetValue(check.Caster.Id, out TimeSpan[]? cooldowns)) cooldowns[check.Slot] = default;
            mana[check.Caster.Id] = Math.Min(HeroOf(check.Caster) is { } hero ? MaxManaOf(check.Caster, hero, LevelOf(check.Caster)) : check.Cost, mana.GetValueOrDefault(check.Caster.Id) + check.Cost);
            Cooldown?.Invoke(check.Caster, check.Slot, 0);
            if (HeroOf(check.Caster) is { } owner) PushMana(check.Caster, owner);
            Notice?.Invoke(check.Caster, "Egzekucja! Zdolność gotowa.");
        }
    }

    private readonly record struct Target(Character Character, Position At, Mob? Mob);

    private IEnumerable<Target> Enemies(Match match, MobDirector director, Faction team)
    {
        foreach (Mob mob in director.Mobs)
        {
            if (mob.Npc is not { IsValid: true } npc || mob.State == MobState.Dead || npc.IsDead || mob.Kind.Faction == team) continue;
            yield return new Target(npc, mob.LastPosition, mob);
        }
        foreach (MatchPlayer other in match.Players.Values)
        {
            if (other.Team == team || server.Players.Find(other.Id) is not { IsValid: true } victim || victim.IsDead || InStasis(victim)) continue;
            yield return new Target(victim, victim.Position, null);
        }
    }

    private static float Distance(Position a, Position b) => a.DistanceTo(b).Centimeters;

    private static float Reach(AbilityDef ability) => MathF.Min(ability.Range, LaneSettings.MaxAttackRange.Centimeters);

    private static float Dot(Position from, Position to, float dx, float dz)
    {
        float x = to.X - from.X, z = to.Z - from.Z;
        float length = MathF.Sqrt(x * x + z * z);
        return length < 1f ? 1f : (x * dx + z * dz) / length;
    }

    private bool Strike(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        Target? target = Enemies(match, director, team)
            .Where(t => Distance(at, t.At) <= Reach(ability) && Dot(at, t.At, dx, dz) >= ConeCos)
            .OrderBy(t => Distance(at, t.At)).Cast<Target?>().FirstOrDefault();
        if (target is not { } hit) { Notice?.Invoke(player, "Brak celu przed tobą."); return false; }
        float distance = Distance(at, hit.At);
        Fx(match, player, hit.Character, 0, ability.Fx);
        Hurt(director, player, hit, ability, now + TimeSpan.FromSeconds(distance / ProjectileSpeed + 0.1));
        return true;
    }

    private bool Nova(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, TimeSpan now)
    {
        Fx(match, player, null, 1, ability.Fx);
        foreach (Target t in Enemies(match, director, team).Where(t => Distance(at, t.At) <= ability.Radius).ToArray())
        {
            Hurt(director, player, t, ability, now + TimeSpan.FromMilliseconds(250));
            if (ability.Seconds > 0) Stun(director, t, now + TimeSpan.FromSeconds(ability.Seconds));
        }
        return true;
    }

    private bool Rain(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        Target? center = Enemies(match, director, team)
            .Where(t => Distance(at, t.At) <= Reach(ability) && Dot(at, t.At, dx, dz) > 0f)
            .OrderBy(t => Distance(at, t.At)).Cast<Target?>().FirstOrDefault();
        if (center is not { } c) { Notice?.Invoke(player, "Brak celu przed tobą."); return false; }
        Fx(match, player, c.Character, 2, ability.Fx);
        foreach (Target t in Enemies(match, director, team).Where(t => Distance(c.At, t.At) <= ability.Radius).ToArray())
            Hurt(director, player, t, ability, now + TimeSpan.FromMilliseconds(700));
        return true;
    }

    private bool Dash(Match match, MobDirector director, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        float range = ability.Range;
        var end = new Position(at.X + dx * range, at.Y, at.Z + dz * range);
        server.Network.Send(player, new AbilityDashMessage(end));
        Fx(match, player, null, 1, ability.Fx);
        if (ability.Damage > 0)
        {
            int steps = Math.Max(2, (int)(MathF.Abs(range) / 150f));
            foreach (Target t in Enemies(match, director, team).ToArray())
            {
                for (int i = 0; i <= steps; i++)
                {
                    float f = i / (float)steps;
                    var point = new Position(at.X + (end.X - at.X) * f, at.Y, at.Z + (end.Z - at.Z) * f);
                    if (Distance(point, t.At) > ability.Radius) continue;
                    Hurt(director, player, t, ability, now + TimeSpan.FromMilliseconds(300));
                    break;
                }
            }
        }
        return true;
    }

    private bool Heal(Match match, Player player, AbilityDef ability, Faction team, Position at)
    {
        Fx(match, player, null, 1, ability.Fx);
        foreach (MatchPlayer ally in match.Players.Values.Where(p => p.Team == team).ToArray())
        {
            if (server.Players.Find(ally.Id) is not { IsValid: true } friend || friend.IsDead) continue;
            if (friend.Id != player.Id && Distance(at, friend.Position) > ability.Radius) continue;
            int healed = (int)Math.Round(HealOf(player, ability) * (1 + ItemsOf(player).HealPower / 100.0));
            friend.Health = Math.Min(friend.MaxHealth, friend.Health + healed);
            if (friend.Id != player.Id) Fx(match, friend, null, 1, ability.Fx);
        }
        return true;
    }

    private bool Shield(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        shields[player.Id] = (now + TimeSpan.FromSeconds(ability.Seconds), Math.Clamp(ability.Damage / 100f, 0f, 0.9f));
        Fx(match, player, null, 1, ability.Fx);
        return true;
    }

    private BuffState Buffs(Player player) => buffs.TryGetValue(player.Id, out BuffState? known) ? known : (buffs[player.Id] = new BuffState());

    private bool Empower(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        state.Primed = true;
        state.PrimedUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.PrimedFx = ability.Fx;
        state.PrimedDamage = DamageOf(player, ability);
        Aura(match, player, ability.Fx, ability.Seconds);
        PushBuffs(player);
        return true;
    }

    private bool Haste(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        state.HasteUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.HasteFx = ability.Fx;
        server.Network.Send(player, new AbilitySpeedMessage(1f + ability.Damage / 100f, (int)(ability.Seconds * 1000)));
        Aura(match, player, ability.Fx, ability.Seconds);
        PushBuffs(player);
        return true;
    }

    private bool Curse(Match match, Player player, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        float ahead = Reach(ability);
        var center = new Position(at.X + dx * ahead, at.Y, at.Z + dz * ahead);
        matches.Send(match, new AbilityGroundMessage(MobCommands.IdOf(player), ability.Fx, center, (int)(ability.Seconds * 1000)));
        zones.Add(new CurseZone(match, player, team, center, ability.Radius, DamageOf(player, ability), now + TimeSpan.FromSeconds(ability.Seconds))
            { NextTick = now + TimeSpan.FromMilliseconds(300) });
        return true;
    }

    private bool Rampage(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        if (state.InRampage(now)) return false;
        int baseHealth = player.MaxHealth;
        int bonus = (int)Math.Round(baseHealth * HeroBuffs.RampageHealth);
        int max = baseHealth + bonus;
        state.RampageBaseHealth = baseHealth;
        state.RampageUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.RampageDamage = ability.Damage / 100f;
        state.RampageFx = ability.Fx;
        player.MaxHealth = max;
        player.Health = Math.Min(max, player.Health + bonus + (int)Math.Round(max * ability.Heal / 100f));
        player.Scale = new GothicOnline.Api.Scale3(HeroBuffs.RampageScale, HeroBuffs.RampageScale, HeroBuffs.RampageScale);
        Aura(match, player, ability.Fx, ability.Seconds);
        PushBuffs(player);
        return true;
    }

    private bool Roll(Match match, Player player, AbilityDef ability, Position at, float dx, float dz, TimeSpan now)
    {
        var end = new Position(at.X + dx * ability.Range, at.Y, at.Z + dz * ability.Range);
        int millis = (int)(ability.Seconds * 1000);
        server.Network.Send(player, new AbilityRollMessage(end, millis));
        Fx(match, player, null, 1, ability.Fx);
        BuffState state = Buffs(player);
        if (state.Hunting(now))
        {
            state.InvisibleUntil = now + TimeSpan.FromMilliseconds(millis) + HeroBuffs.HuntInvisibleAfter;
            player.IsInvisible = true;
            PushBuffs(player);
        }
        return true;
    }

    private bool PierceShot(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        state.Pierce = true;
        state.PierceUntil = now + HeroBuffs.PierceWindow;
        state.PierceFx = ability.Fx;
        state.PierceStun = ability.Seconds;
        server.Network.Send(player, new AbilityAnimMessage(HeroBuffs.PierceAnimation));
        Aura(match, player, ability.Fx, (float)HeroBuffs.PierceWindow.TotalSeconds);
        PushBuffs(player);
        return true;
    }

    private bool HunterMode(Match match, Player player, AbilityDef ability, TimeSpan now)
    {
        BuffState state = Buffs(player);
        state.HuntUntil = now + TimeSpan.FromSeconds(ability.Seconds);
        state.HuntFx = ability.Fx;
        Aura(match, player, ability.Fx, ability.Seconds);
        PushBuffs(player);
        return true;
    }

    private const int SkeletonLimit = 3, GolemLimit = 1;

    private bool Summon(Match match, MobDirector director, Player player, HeroDef hero, AbilityDef ability, Faction team, Position at, float dx, float dz, TimeSpan now)
    {
        MobKind kind = SummonKinds.Of(StatsOf(player) is { } own ? ability with { Damage = HeroResources.AbilityDamage(ability, own), Heal = HeroResources.AbilityHeal(ability, own) } : ability, team);
        var spawn = new Position(at.X + dx * 200f, at.Y, at.Z + dz * 200f);
        Mob pet = director.SpawnCompanion(kind, player, spawn, now, TimeSpan.FromSeconds(ability.Seconds), ability.Key == "golem" ? GolemLimit : SkeletonLimit);
        if (pet.Npc is null) { Notice?.Invoke(player, "Nie udało się przywołać."); return false; }
        Fx(match, player, null, 1, ability.Fx);
        return true;
    }

    private bool Decoy(Match match, MobDirector director, Player player, HeroDef hero, AbilityDef ability, Faction team, Position at, TimeSpan now)
    {
        Position? runTo = null;
        if (moving.TryGetValue(player.Id, out (float X, float Z, TimeSpan At) velocity) && now - velocity.At < TimeSpan.FromSeconds(1))
        {
            float speed = MathF.Sqrt(velocity.X * velocity.X + velocity.Z * velocity.Z);
            if (speed >= RunSpeed) runTo = new Position(at.X + velocity.X / speed * DecoyRunDistance, at.Y, at.Z + velocity.Z / speed * DecoyRunDistance);
        }
        MobKind kind = SummonKinds.CopyOf(hero, team, player.Visual, player.Scale);
        Mob copy = director.SpawnCompanion(kind, player, at, now, TimeSpan.FromSeconds(ability.Seconds), limit: 1, runTo, player.WeaponMode);
        if (copy.Npc is null) { Notice?.Invoke(player, "Nie udało się stworzyć kopii."); return false; }
        Fx(match, player, null, 1, ability.Fx);
        return true;
    }

    private const float RunSpeed = 150f, DecoyRunDistance = 3000f;
    private readonly Dictionary<PlayerId, (float X, float Z, TimeSpan At)> moving = [];
    private readonly Dictionary<PlayerId, (Position At, TimeSpan Time)> lastSample = [];

    private void TrackMovement(TimeSpan now)
    {
        foreach (PlayerId id in mana.Keys)
        {
            if (server.Players.Find(id) is not { IsValid: true } player) continue;
            if (lastSample.TryGetValue(id, out var previous) && now - previous.Time < TimeSpan.FromMilliseconds(300)) continue;
            Position at = player.Position;
            if (lastSample.TryGetValue(id, out previous))
            {
                float seconds = (float)(now - previous.Time).TotalSeconds;
                if (seconds > 0.05f) moving[id] = ((at.X - previous.At.X) / seconds, (at.Z - previous.At.Z) / seconds, now);
            }
            lastSample[id] = (at, now);
        }
    }

    public int ModifyPlayerHit(Player attacker, Character victim, int amount)
    {
        BuffState state = Buffs(attacker);
        TimeSpan now = server.Clock.Elapsed;
        bool rampage = state.InRampage(now);
        Touch(attacker, now);
        bool beast = state.Shadowed(now);
        if (!beast) state.LastNormalHit = amount;
        float total = beast ? Math.Max(10, state.LastNormalHit) * (1f + state.ShadowBonus) : amount;
        HeroDef? hero = HeroOf(attacker);
        Match? fight = matches.Of(attacker);
        if (state.Pierce && now < state.PierceUntil && fight?.Director is { } stunner)
        {
            state.Pierce = false;
            StopAura(attacker, state.PierceFx);
            float seconds = state.Hunting(now) ? HeroBuffs.HuntPierceStun : state.PierceStun;
            Stun(stunner, new Target(victim, victim.Position, stunner.MobOf(victim)), now + TimeSpan.FromSeconds(seconds));
            Fx(fight, attacker, victim, 2, HeroBuffs.PierceHitFx);
            PushBuffs(attacker);
        }
        if (hero is not null && PassiveOf(attacker, AbilityKind.Passive) is { } hunter && fight is not null)
        {
            float huntFactor = HeroBuffs.HuntComboFactor + (hunter.Damage - hero.Abilities.First(a => a.Kind == AbilityKind.Passive).Damage) / 100f;
            bool same = ReferenceEquals(state.ComboTarget, victim) && now - state.ComboAt <= TimeSpan.FromSeconds(hunter.Seconds);
            state.ComboCount = same ? state.ComboCount + 1 : 1;
            state.ComboTarget = victim;
            state.ComboAt = now;
            if (state.ComboCount >= Math.Max(2, (int)hunter.Radius))
            {
                total *= state.Hunting(now) ? huntFactor : hunter.Damage / 100f;
                state.ComboCount = 0;
                Fx(fight, attacker, victim, 2, hunter.Fx);
            }
            PushBuffs(attacker);
        }
        if (state.Primed && now < state.PrimedUntil && matches.Of(attacker) is { } match && match.Players.TryGetValue(attacker.Id, out MatchPlayer? mp)
            && HeroOf(attacker)?.Abilities.Any(a => a.Kind == AbilityKind.Empower) == true)
        {
            float stacks = mp.Stacks * (rampage ? HeroBuffs.RampageStackFactor : 1f);
            total += state.PrimedDamage + stacks * HeroBuffs.StackDamage;
            mp.Stacks += 1;
            state.Primed = false;
            StopAura(attacker, state.PrimedFx);
            Fx(match, attacker, victim, 2, HeroBuffs.EmpowerHitFx);
            PushBuffs(attacker);
        }
        if (rampage) total *= 1f + state.RampageDamage;
        if (now < state.BloodUntil && state.BloodDrain > 0 && fight is not null && (victim is Player || fight.Director?.MobOf(victim) is { Champion: true }))
        {
            total += state.BloodDrain;
            attacker.Health = Math.Min(attacker.MaxHealth, attacker.Health + state.BloodDrain);
            state.BloodUntil = default;
            StopAura(attacker, state.BloodFx);
            PushBuffs(attacker);
        }
        if (hero is not null && PassiveOf(attacker, AbilityKind.Lightning) is { } lightning && fight is not null
            && (victim is Player || fight.Director?.MobOf(victim) is { Champion: true }) && total > 0)
        {
            total += total * lightning.Damage / 100f;
            Fx(fight, attacker, victim, 2, lightning.Fx);
        }
        if (hero is { Bleeds: true } && fight?.Director is { } bleedDirector && total > 0) ApplyBleed(attacker, victim, bleedDirector, fight);
        if (fight?.Director is { } burnDirector && total > 0) TryIgnite(attacker, victim, burnDirector);
        if (fight?.Director is { } scorchDirector && total > 0) TryScorch(attacker, victim, scorchDirector, state, now);
        int dealt = (int)Math.Round(total);
        LeechHeal(attacker, dealt, basicHit: true);
        return dealt;
    }

    private sealed class Bleed(Player source, Character victim, MobDirector director)
    {
        public Player Source { get; } = source;
        public Character Victim { get; } = victim;
        public MobDirector Director { get; } = director;
        public int Stacks;
        public float PerStack;
        public TimeSpan Until, NextTick;
    }

    private readonly List<Bleed> bleeds = [];

    private void ApplyBleed(Player source, Character victim, MobDirector director, Match match)
    {
        Mob? mob = director.MobOf(victim);
        if (mob is { Kind.Immobile: true } || !victim.IsValid || victim.IsDead || ReferenceEquals(source, victim)) return;
        TimeSpan now = server.Clock.Elapsed;
        Bleed? bleed = bleeds.FirstOrDefault(b => ReferenceEquals(b.Victim, victim) && b.Source.Id == source.Id);
        if (bleed is null) { bleed = new Bleed(source, victim, director) { NextTick = now + TimeSpan.FromSeconds(1) }; bleeds.Add(bleed); }
        bleed.Stacks = Math.Min(HeroBuffs.BleedMaxStacks, bleed.Stacks + 1);
        bleed.Until = now + TimeSpan.FromSeconds(HeroBuffs.BleedSeconds);
        int ad = StatsOf(source)?.Ad ?? 0;
        bleed.PerStack = (HeroBuffs.BleedBase + HeroBuffs.BleedPerLevel * LevelOf(source) + HeroBuffs.BleedAdRatio * ad) / HeroBuffs.BleedSeconds;
        Fx(match, source, victim, 2, HeroBuffs.BleedFx);
        if (victim is Player bleeding) { BuffState state = Buffs(bleeding); state.BleedStacks = bleed.Stacks; state.BleedUntil = bleed.Until; PushBuffs(bleeding); }
    }

    private void UpdateBleeds(TimeSpan now)
    {
        for (int i = bleeds.Count - 1; i >= 0; i--)
        {
            Bleed bleed = bleeds[i];
            bool over = !bleed.Victim.IsValid || bleed.Victim.IsDead || !bleed.Source.IsValid || now >= bleed.Until;
            if (!over && now >= bleed.NextTick)
            {
                bleed.NextTick += TimeSpan.FromSeconds(1);
                int amount = (int)Math.Round(bleed.Stacks * bleed.PerStack);
                if (amount > 0)
                {
                    if (bleed.Director.MobOf(bleed.Victim) is not null) bleed.Director.ScheduleDamage(bleed.Victim, amount, now, bleed.Source, magic: false);
                    else pending.Add(new Pending(now, bleed.Victim, amount, bleed.Source, Magic: false));
                }
            }
            if (!over) continue;
            bleeds.RemoveAt(i);
            if (bleed.Victim is Player { IsValid: true } cured && buffs.TryGetValue(cured.Id, out BuffState? state)
                && !bleeds.Any(b => ReferenceEquals(b.Victim, cured)))
            {
                state.BleedStacks = 0;
                state.BleedUntil = default;
                PushBuffs(cured);
            }
        }
    }

    private void EndBuffs(Player player)
    {
        if (!buffs.Remove(player.Id, out BuffState? state)) return;
        if (state.RampageBaseHealth > 0) EndRampage(player, state);
        if (state.ShadowOwnInstance is not null) EndShadow(player, state);
        if (state.HasteUntil > server.Clock.Elapsed) server.Network.Send(player, new AbilitySpeedMessage(1f, 0));
        foreach (string fx in new[] { state.PrimedFx, state.HasteFx, state.RampageFx, state.PierceFx, state.HuntFx, state.IronFx, state.BloodFx, state.FireFx }) StopAura(player, fx);
        if (state.InvisibleUntil != default && player.IsValid) player.IsInvisible = false;
        PushBuffs(player);
    }

    private void EndRampage(Player player, BuffState state)
    {
        if (!player.IsValid) { state.RampageBaseHealth = 0; return; }
        int baseHealth = state.RampageBaseHealth;
        state.RampageBaseHealth = 0;
        player.Scale = new GothicOnline.Api.Scale3(1f, 1f, 1f);
        player.MaxHealth = baseHealth;
        if (!player.IsDead && player.Health > baseHealth) player.Health = baseHealth;
    }

    private void UpdateBuffs(TimeSpan now)
    {
        foreach ((PlayerId id, BuffState state) in buffs.ToArray())
        {
            if (server.Players.Find(id) is not { IsValid: true } player) { buffs.Remove(id); continue; }
            bool changed = false;
            if (state.Primed && now >= state.PrimedUntil)
            {
                state.Primed = false;
                StopAura(player, state.PrimedFx);
                changed = true;
            }
            if (state.RampageBaseHealth > 0 && now >= state.RampageUntil)
            {
                EndRampage(player, state);
                StopAura(player, state.RampageFx);
                changed = true;
            }
            if (state.HasteFx.Length > 0 && now >= state.HasteUntil)
            {
                StopAura(player, state.HasteFx);
                state.HasteFx = "";
                changed = true;
            }
            if (state.Pierce && now >= state.PierceUntil)
            {
                state.Pierce = false;
                StopAura(player, state.PierceFx);
                changed = true;
            }
            if (state.HuntFx.Length > 0 && now >= state.HuntUntil)
            {
                StopAura(player, state.HuntFx);
                state.HuntFx = "";
                changed = true;
            }
            if (state.InvisibleUntil != default && now >= state.InvisibleUntil)
            {
                state.InvisibleUntil = default;
                player.IsInvisible = false;
                changed = true;
            }
            if (state.ShadowOwnInstance is not null && now >= state.ShadowUntil) { EndShadow(player, state); changed = true; }
            if (state.IronFx.Length > 0 && now >= state.IronUntil) { StopAura(player, state.IronFx); state.IronFx = ""; changed = true; }
            if (state.BloodFx.Length > 0 && now >= state.BloodUntil) { StopAura(player, state.BloodFx); state.BloodFx = ""; state.BloodDrain = 0; changed = true; }
            if (state.ComboCount > 0 && now - state.ComboAt > TimeSpan.FromSeconds(4))
            {
                state.ComboCount = 0;
                changed = true;
            }
            if (changed) PushBuffs(player);
        }
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            CurseZone zone = zones[i];
            if (now >= zone.Until || zone.Match.Director is not { } director || !zone.Caster.IsValid) { zones.RemoveAt(i); continue; }
            if (now < zone.NextTick) continue;
            zone.NextTick = now + HeroBuffs.CurseTick;
            int damage = zone.Damage;
            if (buffs.TryGetValue(zone.Caster.Id, out BuffState? state) && state.InRampage(now)) damage = (int)Math.Round(damage * (1f + state.RampageDamage));
            foreach (Target t in Enemies(zone.Match, director, zone.Team).Where(t => Flat(zone.At, t.At) <= zone.Radius).ToArray())
            {
                if (TowerOutOfReach(zone.Caster.Position, t)) continue;
                if (t.Mob is not null) director.ScheduleDamage(t.Character, damage, now, zone.Caster, magic: true);
                else pending.Add(new Pending(now, t.Character, damage, zone.Caster, Magic: true));
            }
        }
    }

    private static float Flat(Position a, Position b)
    {
        float x = a.X - b.X, z = a.Z - b.Z;
        return MathF.Sqrt(x * x + z * z);
    }

    private void StopAura(Player player, string fx)
    {
        if (fx.Length > 0 && player.IsValid && matches.Of(player) is { } match) matches.Send(match, new AbilityAuraMessage(MobCommands.IdOf(player), fx, 0));
    }

    private void Aura(Match match, Player player, string fx, float seconds) =>
        matches.Send(match, new AbilityAuraMessage(MobCommands.IdOf(player), fx, (int)(seconds * 1000)));

    private void PushBuffs(Player player)
    {
        if (!player.IsValid) return;
        TimeSpan now = server.Clock.Elapsed;
        float stacks = matches.Of(player)?.Players.GetValueOrDefault(player.Id)?.Stacks ?? 0f;
        BuffState? state = buffs.GetValueOrDefault(player.Id);
        int Left(TimeSpan until) => until > now ? (int)(until - now).TotalMilliseconds : 0;
        BuffsChanged?.Invoke(player, new
        {
            stacks = (int)stacks,
            stackDamage = HeroBuffs.StackDamage,
            primed = state is { Primed: true } && now < state.PrimedUntil ? Left(state.PrimedUntil) : 0,
            haste = state is null ? 0 : Left(state.HasteUntil),
            rampage = state is not null && state.InRampage(now) ? Left(state.RampageUntil) : 0,
            pierce = state is { Pierce: true } ? Left(state.PierceUntil) : 0,
            hunt = state is null ? 0 : Left(state.HuntUntil),
            hidden = state is null || state.InvisibleUntil == default ? 0 : Left(state.InvisibleUntil),
            combo = state?.ComboCount ?? 0,
            iron = state is null ? 0 : Left(state.IronUntil),
            shadow = state is not null && state.Shadowed(now) ? Left(state.ShadowUntil) : 0,
            blood = state is null ? 0 : Left(state.BloodUntil),
            bleed = state is null ? 0 : Left(state.BleedUntil),
            bleedStacks = state is not null && now < state.BleedUntil ? state.BleedStacks : 0,
            fire = state is { FireCharges: > 0 } ? Left(state.FireUntil) : 0,
            fireCharges = state?.FireCharges ?? 0,
            tribe = state?.TribeStacks ?? 0,
            guard = shields.TryGetValue(player.Id, out var guardShield) && guardShield.Reduction >= 1f ? Left(guardShield.Until) : 0,
        });
    }

    private readonly Dictionary<PlayerId, float> healthRegen = [];
    private TimeSpan nextHealthRegen;

    private void RegenerateHealth(TimeSpan now)
    {
        if (now < nextHealthRegen) return;
        nextHealthRegen = now + TimeSpan.FromSeconds(1);
        foreach (PlayerId id in mana.Keys.ToArray())
        {
            if (server.Players.Find(id) is not { IsValid: true, IsDead: false } player || StatsOf(player) is not { } stats) { healthRegen.Remove(id); continue; }
            if (player.Health <= 0 || player.Health >= player.MaxHealth) { healthRegen[id] = 0f; continue; }
            float total = healthRegen.GetValueOrDefault(id) + stats.Regen / 5f * 0.5f;
            if (AtBase(player)) total += player.MaxHealth * BaseHealPerSecond;
            int whole = (int)total;
            healthRegen[id] = total - whole;
            if (whole > 0) player.Health = Math.Min(player.MaxHealth, player.Health + whole);
        }
    }

    private void RegenerateMana(TimeSpan now)
    {
        bool resync = now >= nextManaPush;
        if (resync) nextManaPush = now + TimeSpan.FromSeconds(3);
        foreach (PlayerId id in mana.Keys.ToArray())
        {
            if (server.Players.Find(id) is not { IsValid: true } player || HeroOf(player) is not { } hero) { mana.Remove(id); continue; }
            if (player.IsDead) continue;
            int level = LevelOf(player);
            double max = MaxManaOf(player, hero, level);
            mana[id] = Math.Min(max, mana[id] + HeroResources.ManaRegen(hero, level) * (1 + ItemsOf(player).ManaRegen / 100.0) * 0.1);
            if (resync) PushMana(player, hero);
        }
    }

    private void Fx(Match match, Player caster, Character? target, int kind, string fx) =>
        matches.Send(match, new AbilityFxMessage(MobCommands.IdOf(caster), target is null ? -1 : MobCommands.IdOf(target), kind, fx));

    private int DamageOf(Player player, AbilityDef ability) => StatsOf(player) is { } stats ? HeroResources.AbilityDamage(ability, stats) : ability.Damage;
    private int HealOf(Player player, AbilityDef ability) => StatsOf(player) is { } stats ? HeroResources.AbilityHeal(ability, stats) : ability.Heal;

    private void Hurt(MobDirector director, Player attacker, Target target, AbilityDef ability, TimeSpan due, float factor = 1f)
    {
        if (HeroOf(attacker) is { } hero && StatsOf(attacker) is { } stats)
            DealDamage(director, attacker, target, HeroResources.AbilityDamage(ability, stats), HeroResources.IsMagicAbility(hero, ability), stats, due, factor);
        else
            DealDamage(director, attacker, target, ability.Damage, false, null, due, factor);
    }

    private void Hurt(MobDirector director, Player attacker, Target target, int amount, TimeSpan due, float factor = 1f)
    {
        if (HeroOf(attacker) is { } hero && StatsOf(attacker) is { } stats)
            DealDamage(director, attacker, target, HeroResources.Scaled(hero, stats, amount), HeroResources.IsMagic(hero), stats, due, factor);
        else
            DealDamage(director, attacker, target, amount, false, null, due, factor);
    }

    private static bool TowerOutOfReach(Position caster, Target target) =>
        target.Mob is { Kind.Structure: StructureKind.Turret }
        && Distance(caster, target.At) > LaneSettings.TowerHitRange.Centimeters + LaneSettings.AttackRangeSlack;

    private void DealDamage(MobDirector director, Player attacker, Target target, int amount, bool magic, HeroStats? stats, TimeSpan due, float factor)
    {
        if (amount <= 0 || TowerOutOfReach(attacker.Position, target)) return;
        Touch(attacker, server.Clock.Elapsed);
        if (stats is { } own && Random.Shared.Next(100) < own.Crit) amount = (int)Math.Round(amount * CritFactor(attacker));
        if (buffs.TryGetValue(attacker.Id, out BuffState? state) && state.InRampage(server.Clock.Elapsed))
            amount = (int)Math.Round(amount * (1f + state.RampageDamage));
        amount = (int)Math.Round(amount * factor);
        if (amount <= 0) return;
        LeechHeal(attacker, amount, basicHit: false);
        TryIgnite(attacker, target.Character, director);
        if (target.Mob is not null) director.ScheduleDamage(target.Character, amount, due, attacker, magic);
        else pending.Add(new Pending(due, target.Character, amount, attacker, Magic: magic));
    }

    public void Freeze(Player victim, TimeSpan until)
    {
        if (!victim.IsValid || victim.IsDead || IsImmune(victim) || InStasis(victim)) return;
        int tenacity = Math.Clamp(ItemsOf(victim).Ten, 0, 90);
        TimeSpan now = server.Clock.Elapsed;
        if (tenacity > 0) until = now + (until - now) * (1.0 - tenacity / 100.0);
        server.Network.Send(victim, new ArenaFreezeMessage(true));
        pending.Add(new Pending(until, victim, 0, null, Unfreeze: true));
    }

    public void SlowHero(Player victim, float scale, int millis)
    {
        if (!victim.IsValid || victim.IsDead || InStasis(victim)) return;
        server.Network.Send(victim, new AbilitySpeedMessage(Math.Clamp(scale, 0.2f, 1f), millis));
    }

    public void DragTo(Character who, Mob? mob, Position to, TimeSpan duration)
    {
        if (!who.IsValid || who.IsDead || (who is Player player && (IsImmune(player) || InStasis(player)))) return;
        TimeSpan now = server.Clock.Elapsed;
        drags.RemoveAll(d => ReferenceEquals(d.Who, who));
        drags.Add(new Drag(who, mob, who.Position, to, now, now + duration));
    }

    private void Stun(MobDirector director, Target target, TimeSpan until)
    {
        if (target.Character is Player protectedHero && (IsImmune(protectedHero) || InStasis(protectedHero))) return;
        if (target.Mob is { } mob) director.Stun(mob, until);
        else if (target.Character is Player victim)
        {
            int tenacity = Math.Clamp(ItemsOf(victim).Ten, 0, 90);
            if (tenacity > 0) { TimeSpan now = server.Clock.Elapsed; until = now + (until - now) * (1.0 - tenacity / 100.0); }
            server.Network.Send(victim, new ArenaFreezeMessage(true));
            pending.Add(new Pending(until, victim, 0, null, Unfreeze: true));
        }
    }

    private void Update()
    {
        TimeSpan now = server.Clock.Elapsed;
        RunLater(now);
        RegenerateMana(now);
        RegenerateHealth(now);
        UpdateRecalls(now);
        TrackMovement(now);
        UpdateBrewers(now);
        UpdateDrags(now);
        UpdateChecks(now);
        UpdateBleeds(now);
        UpdateBurns(now);
        UpdateUrshak(now);
        for (int i = spinHits.Count - 1; i >= 0; i--)
        {
            SpinHit spin = spinHits[i];
            if (spin.Due > now) continue;
            spinHits.RemoveAt(i);
            SweepSpin(spin, now);
        }
        for (int i = mights.Count - 1; i >= 0; i--)
        {
            MightHit might = mights[i];
            if (might.Due > now) continue;
            mights.RemoveAt(i);
            MightImpact(might, now);
        }
        for (int i = landings.Count - 1; i >= 0; i--)
        {
            Landing landing = landings[i];
            if (landing.Due > now) continue;
            landings.RemoveAt(i);
            Land(landing, now);
        }
        UpdateBuffs(now);
        if (pending.Count == 0) return;
        foreach (Pending item in pending.Where(p => p.Due <= now).ToArray())
        {
            pending.Remove(item);
            if (item.Target is not Player { IsValid: true } victim) continue;
            if (item.Unfreeze) { server.Network.Send(victim, new ArenaFreezeMessage(false)); continue; }
            if (victim.IsDead) continue;
            int amount = item.Amount;
            if (StatsOf(victim) is { } defence) amount = (int)Math.Round(amount * HeroResources.Mitigation(Penetrated(item.Magic ? defence.Resist : defence.Armor, item.Attacker, item.Magic)));
            if (shields.TryGetValue(victim.Id, out var shield) && now < shield.Until) amount = (int)Math.Round(amount * (1f - shield.Reduction));
            victim.Health = Math.Max(0, victim.Health - amount);
            if (item.Attacker is not null) lastHit[victim.Id] = (item.Attacker.Id, now);
            matches.Of(victim)?.Director?.RecordAggression(item.Attacker, victim, amount, item.Magic);
        }
    }
}
