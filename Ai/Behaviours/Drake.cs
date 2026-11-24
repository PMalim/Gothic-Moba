namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Drake() => new("smok",
        new Perception(aggressive: false, pursuit: 1.3f, playerRange: DrakeSettings.Aggro.Centimeters),
        new TargetMemory(TimeSpan.FromSeconds(5)),
        new Territory(DrakeSettings.Leash, grace: TimeSpan.Zero, committed: true),
        new DrakeCombat(),
        new DrakeReset());
}
