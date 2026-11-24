namespace BotArena.Ai;

public sealed class MobBrain(MobCommands commands, Random random, MobDirector director)
{
    private static readonly TimeSpan StuckQueue = TimeSpan.FromSeconds(10);
    private const int MaxPasses = 3;
    public static readonly TimeSpan ShortMemory = TimeSpan.FromSeconds(1.5);

    public void Think(Mob mob, NpcState state, TimeSpan now, IReadOnlyList<Combatant> players, IReadOnlyList<Combatant> mobs, IReadOnlyDictionary<Character, Combatant> index)
    {
        Npc npc = mob.Npc!;
        mob.LastThink = now;
        mob.LastPosition = state.Position;
        mob.LastHealth = state.Health;
        mob.LastMaxHealth = state.MaxHealth;
        mob.QueuedActions = state.PendingActions;
        mob.Note = "";
        if (now < mob.StunnedUntil) { mob.Note = "ogłuszony"; return; }
        MobBehaviour behaviour = mob.Behaviour;
        var context = new MobContext
        {
            Mob = mob, Npc = npc, State = state, Now = now, Players = players, Mobs = mobs, Index = index,
            Commands = commands, Random = random, Director = director,
        };
        if (state.Host is null && !mob.Kind.Immobile)
        {
            if (mob.State != MobState.Dormant) behaviour.Tree.Abort(context);
            if (mob.HadHost && state.PendingActions > 0) npc.Actions.Clear();
            mob.CurrentAction = null;
            mob.HadHost = false;
            mob.State = MobState.Dormant;
            foreach (MobComponent component in behaviour.Components) component.Dormant(context);
            return;
        }
        if (!mob.HadHost)
        {
            mob.HadHost = true;
            mob.State = MobState.Active;
        }
        if (state.PendingActions > 3)
        {
            mob.QueueBusySince ??= now;
            if (now - mob.QueueBusySince.Value > StuckQueue)
            {
                behaviour.Tree.Abort(context);
                npc.Actions.Clear();
                mob.CurrentAction = null;
                mob.QueueBusySince = null;
                mob.Note = "kolejka akcji utknęła — wyczyszczona";
                return;
            }
        }
        else mob.QueueBusySince = null;

        Sense(context);
        context.Target = behaviour.Fights ? SelectTarget(context) : null;
        mob.Target = context.Target?.Character;
        if (mob.Target is not null) mob.Quarry = mob.Target;
        foreach (MobComponent component in behaviour.Components) component.React(context);

        for (int pass = 0; pass < MaxPasses; pass++)
        {
            context.Replan = false;
            behaviour.Tree.Tick(context);
            if (!context.Replan) break;
        }
    }

    public static WeaponMode CombatMode(MobKind kind) =>
        kind.Behaviour.Has<RangedCombat>() ? WeaponMode.Bow : kind.Humanoid ? kind.MeleeMode : WeaponMode.Fists;

    private static void Sense(MobContext c)
    {
        c.Memory.BeginSense();
        foreach (MobComponent component in c.Behaviour.Components) component.Sense(c);
        float feel = c.Kind.SightRange.Centimeters * 2;
        foreach (KnownCharacter known in c.Memory.Known)
        {
            if (known.Visible || known.Threat <= 0 || c.Find(known.Character) is not { } attacker) continue;
            if (attacker.Mob is { Kind.Immobile: true } guarded && c.Director.IsProtected(guarded)) continue;
            if (c.Position.DistanceTo(attacker.Position).Centimeters <= feel) c.Memory.See(attacker.Character, attacker.Position, c.Now);
        }
        TimeSpan retention = c.Behaviour.Get<TargetMemory>()?.Remember ?? ShortMemory;
        c.Memory.Prune(character => c.Find(character) is not null, c.Now, retention);
    }

    private static Combatant? SelectTarget(MobContext c)
    {
        Combatant? best = null;
        float bestScore = float.MinValue;
        float range = c.Kind.SightRange.Centimeters * 2;
        foreach (KnownCharacter known in c.Memory.Known)
        {
            if (!known.Visible || c.Find(known.Character) is not { } candidate) continue;
            if (candidate.Mob is { Kind.Immobile: true } structure && c.Director.IsProtected(structure)) continue;
            float distance = c.Position.DistanceTo(candidate.Position).Centimeters;
            bool current = ReferenceEquals(c.Mob.Target, known.Character);
            float score = (candidate.IsPlayer && c.Mob.Lane is null ? 100 : 0) + MathF.Max(0, 1 - distance / range) * 100 + known.Threat + (current ? 40 : 0);
            if (score > bestScore) { bestScore = score; best = candidate; }
        }
        return best;
    }
}
