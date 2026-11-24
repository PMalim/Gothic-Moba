namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Summoned() => new("pomocnik",
        new Perception(pursuit: 1.5f, players: true, mobRange: 1400, playerRange: 1400),
        new TargetMemory(TimeSpan.FromSeconds(3)),
        new Companion(),
        new MeleeCombat(circles: false));

    public static MobBehaviour DecoyCopy() => new("kopia", new Decoy());
}
