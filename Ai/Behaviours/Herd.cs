namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Herd(float flee = 0.35f, float wander = 600, float leash = 2800) => new("stado roślinożerców",
        new Perception(aggressive: false),
        new TargetMemory(TimeSpan.FromSeconds(6)),
        new Grudge(TimeSpan.FromMinutes(10)),
        new Territory(new Distance(leash)),
        new Flee(flee),
        new Pack(flank: false, hunts: false),
        new MeleeCombat(),
        new Regeneration(),
        new Wander(wander));
}
