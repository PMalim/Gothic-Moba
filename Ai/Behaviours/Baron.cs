namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Baron() => new("baron",
        new Perception(aggressive: false, pursuit: 1.15f, playerRange: BaronSettings.Aggro.Centimeters),
        new TargetMemory(TimeSpan.FromSeconds(5)),
        new BaronCombat(),
        new BaronReset());
}
