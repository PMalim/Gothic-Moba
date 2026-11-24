namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour Hunter(float flee = 0, float remember = 10, float wander = 350, float leash = 2800, bool circles = false)
    {
        var components = new List<MobComponent>
        {
            new Perception(),
            new TargetMemory(TimeSpan.FromSeconds(remember)),
            new Territory(new Distance(leash)),
            new MeleeCombat(circles),
            new Search(),
            new Alert(),
            new Regeneration(),
            new Wander(wander),
        };
        if (flee > 0) components.Insert(3, new Flee(flee));
        return new MobBehaviour("łowca", [.. components]);
    }
}
