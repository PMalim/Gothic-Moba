namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Guard(float leash = 1800) => new("strażnik",
        new Perception(pursuit: 1.5f),
        new TargetMemory(TimeSpan.FromSeconds(3)),
        new Territory(new Distance(leash)),
        new MeleeCombat(),
        new Regeneration(0.04f),
        new Wander(stroll: false));
}
