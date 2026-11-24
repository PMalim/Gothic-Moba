namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Archer(float preferred = 1100, float flee = 0.3f, float remember = 10, float leash = 2800) => new("strzelec",
        new Perception(),
        new TargetMemory(TimeSpan.FromSeconds(remember)),
        new Territory(new Distance(leash)),
        new Flee(flee),
        new Pack(flank: false),
        new RangedCombat(new Distance(preferred)),
        new Search(sweeps: 1),
        new Alert(),
        new Regeneration(),
        new Wander(250));
}
