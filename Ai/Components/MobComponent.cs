namespace BotArena.Ai.Components;

public readonly record struct Branch(int Priority, Node<MobContext> Node);

public static class Priority
{
    public const int Flee = 100;
    public const int Leash = 200;
    public const int Combat = 300;
    public const int Search = 400;
    public const int Lane = 450;
    public const int Alert = 500;
}

public abstract class MobComponent
{
    public abstract string Name { get; }

    public virtual void Sense(MobContext c) { }
    public virtual void Dormant(MobContext c) { }
    public virtual void React(MobContext c) { }
    public virtual IEnumerable<Branch> Branches() => [];
    public virtual IEnumerable<Node<MobContext>> Idle() => [];
    public virtual void Calm(MobContext c) { }
    public virtual void Damaged(MobHit hit) { }
    public virtual void Died(Mob mob, Character? killer) { }
    public virtual float IncomingDamage(Mob mob, Character? attacker, TimeSpan now) => 1f;
}

public readonly record struct MobHit(Mob Mob, Character Attacker, Position? AttackerPosition, int Amount, TimeSpan Now, MobDirector Director);
