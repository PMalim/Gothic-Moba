namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Jungle(bool leader = false) => new("bestia z dżungli",
        new Perception(aggressive: false, pursuit: 2f),
        new TargetMemory(TimeSpan.FromSeconds(6)),
        new Territory(new Distance(JungleSettings.LeashCm), grace: TimeSpan.Zero, committed: true),
        new MeleeCombat(),
        new Pack(flank: true, hunts: true, leader),
        new CampReset());
}
