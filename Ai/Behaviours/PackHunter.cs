namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour PackHunter(float flee = 0, float remember = 12, float wander = 350, float leash = 2800,
        bool leader = false, bool flank = true, bool circles = true) =>
        Hunter(leader ? 0 : flee, remember, wander, leash, circles)
            .With(new Pack(flank, hunts: true, leader))
            .Named(leader ? "przywódca stada" : "łowca stadny");
}
