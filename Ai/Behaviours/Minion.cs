namespace BotArena.Ai.Behaviours;

public static partial class MobBehaviours
{
    public static MobBehaviour LaneMelee(float remember = 3) => new("minion wręcz",
        LanePerception(),
        new TargetMemory(TimeSpan.FromSeconds(remember)),
        new MeleeCombat(circles: false),
        new LaneMarch());

    public static MobBehaviour LaneCaster(float remember = 3) => new("minion szaman",
        LanePerception(),
        new TargetMemory(TimeSpan.FromSeconds(remember)),
        new BoltCombat(LaneSettings.SpellRange, LaneSettings.SpellCooldown),
        new LaneMarch());

    public static MobBehaviour LaneCannon(float remember = 3) => new("minion kanonier",
        LanePerception(),
        new TargetMemory(TimeSpan.FromSeconds(remember)),
        new BoltCombat(LaneSettings.CannonRange, LaneSettings.CannonCooldown, LaneSettings.CannonBallSpeed, heavy: true),
        new LaneMarch());

    public static MobBehaviour Turret() => new("wieża",
        new Perception(players: LaneSettings.AttackPlayers, mobRange: LaneSettings.TurretSight.Centimeters,
            playerRange: LaneSettings.TurretSight.Centimeters),
        new TurretGun(LaneSettings.TurretSight, LaneSettings.TurretCooldown));

    public static MobBehaviour Structure() => new("struktura");

    public static MobBehaviour Dragon() => new("smok", new DragonRay(LaneSettings.DragonRange, LaneSettings.DragonCooldown));

    private static Perception LanePerception() =>
        new(pursuit: 1.5f, players: LaneSettings.AttackPlayers, mobRange: LaneSettings.EngageRange.Centimeters,
            playerRange: LaneSettings.PlayerNoticeRange);
}
