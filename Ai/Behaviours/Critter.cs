namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Critter(float wander = 400) => new("zwierzyna",
        new Flee(whenHurt: true),
        new Regeneration(),
        new Wander(wander));
}
