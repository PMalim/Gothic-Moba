namespace BotArena.Ai.Components;

public sealed class Perception(bool aggressive = true, float pursuit = 2f, bool players = true, float? mobRange = null, float? playerRange = null)
    : MobComponent
{
    public override string Name => aggressive ? "wzrok" : "wzrok (nie zaczepia)";
    public bool Aggressive => aggressive;
    public Distance PursuitRange(MobKind kind) => new(kind.SightRange.Centimeters * pursuit);

    public override void Sense(MobContext c)
    {
        float sight = c.Kind.SightRange.Centimeters, keep = sight * pursuit;
        if (players)
        {
            float noticePlayer = playerRange ?? sight, keepPlayer = playerRange is { } range ? range * pursuit : keep;
            foreach (Combatant player in c.Players)
            {
                if (Teams.Untargetable(player.Character)) continue;
                Look(c, player, noticePlayer, keepPlayer, aggressive && Teams.IsEnemy(c.Kind.Faction, player.Character));
            }
        }
        foreach (Combatant other in c.Mobs)
        {
            if (other.Mob is not { } mob || ReferenceEquals(mob, c.Mob)) continue;
            if (mob.Camp is not null && mob.Camp == c.Mob.Camp) continue;
            bool feud = aggressive && Factions.Hostile(c.Kind.Faction, mob.Kind.Faction);
            if (!feud && c.Memory.Recall(other.Character) is null) continue;
            if (mob.Kind.Immobile && c.Director.IsProtected(mob)) continue;
            float notice = MathF.Min(sight, mobRange ?? ArenaSettings.FeudRange.Centimeters);
            if (feud && mob.Kind.Immobile && !c.Kind.Immobile && c.Mob.Lane is { } lane
                && (ReferenceEquals(mob.Lane, lane) || mob.Tier == 0 || mob.Kind.Structure == StructureKind.Nexus))
                notice = MathF.Max(notice, LaneSettings.StructureNotice.Centimeters);
            Look(c, other, notice, MathF.Max(keep, notice), feud);
        }
    }

    private static void Look(MobContext c, Combatant who, float notice, float keep, bool hostile)
    {
        if (c.Memory.IsIgnored(who.Character, c.Now)) return;
        KnownCharacter? known = c.Memory.Recall(who.Character);
        bool interested = hostile || known is { Threat: > 0 } || known is { Grudge: true };
        if (!interested) return;
        float distance = c.Position.DistanceTo(who.Position).Centimeters;
        if (known is null)
        {
            if (distance > notice) return;
            if (MathF.Abs(who.Position.Y - c.Position.Y) > ArenaSettings.MaxHeightDifference.Centimeters) return;
        }
        else if (distance > keep) return;
        c.Memory.See(who.Character, who.Position, c.Now);
    }
}

public sealed class TargetMemory(TimeSpan remember) : MobComponent
{
    public override string Name => $"pamięć {remember.TotalSeconds:0} s";
    public TimeSpan Remember => remember;
}
