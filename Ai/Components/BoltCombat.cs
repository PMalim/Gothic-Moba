namespace BotArena.Ai.Components;

public sealed class BoltCombat(Distance range, TimeSpan cooldown, float speed = LaneSettings.BoltSpeed, bool heavy = false) : MobComponent
{
    public override string Name => $"laser maga {range.Centimeters / 100:0} m";
    public Distance Range => range;
    public TimeSpan Cooldown => cooldown;
    public float Speed => speed;
    public bool Heavy => heavy;

    public override IEnumerable<Branch> Branches()
    {
        yield return new Branch(Priority.Combat, Combat.Fight(_ => range,
            new Selector<MobContext>("walka laserem",
                new Sequence<MobContext>("strzał",
                    new Condition<MobContext>("w zasięgu strzału?", c => c.TargetDistance <= range),
                    new Selector<MobContext>("strzela albo celuje",
                        new Sequence<MobContext>("strzela",
                            new Condition<MobContext>("gotowy do strzału?", c => c.Now >= c.Get<CombatState>().NextAttack),
                            new Do<MobContext>("strzela laserem", c =>
                            {
                                c.Get<CombatState>().NextAttack = c.Now + c.Jitter(cooldown);
                                c.Mob.LastCombat = c.Now;
                                Combat.FaceTarget(c);
                                if (c.Kind.Damage is not null || c.Kind.Role is not null)
                                    c.Director.BoltFire(c.Mob, c.Target!.Value.Character, c.Director.AttackDamage(c.Mob, c.Target!.Value.Character), c.Now, speed);
                                return NodeStatus.Success;
                            })),
                        new Do<MobContext>("celuje", Combat.FaceTarget))),
                Combat.Approach("podchodzi", _ => new Distance(range.Centimeters * 0.85f)))));
    }
}
