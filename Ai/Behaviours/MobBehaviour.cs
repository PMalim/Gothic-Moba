namespace BotArena.Ai.Behaviours;

public sealed class MobBehaviour
{
    private readonly MobComponent[] components;
    private BehaviorTree<MobContext>? tree;

    public MobBehaviour(string name, params MobComponent[] components)
    {
        Name = name;
        this.components = components;
    }

    public string Name { get; }
    public IReadOnlyList<MobComponent> Components => components;
    public BehaviorTree<MobContext> Tree => tree ??= Build();
    public bool Fights => components.Any(component => component is MeleeCombat or RangedCombat or CastCombat or BoltCombat or TurretGun or DragonRay or DrakeCombat or BaronCombat);

    public T? Get<T>() where T : MobComponent => components.OfType<T>().FirstOrDefault();
    public bool Has<T>() where T : MobComponent => Get<T>() is not null;

    public MobBehaviour With(params MobComponent[] extra) =>
        new(Name, [.. components.Where(existing => !extra.Any(added => added.GetType() == existing.GetType())), .. extra]);
    public MobBehaviour Without<T>() where T : MobComponent => new(Name, [.. components.Where(component => component is not T)]);
    public MobBehaviour Named(string name) => new(name, components);

    public string Describe() => string.Join(", ", components.Select(component => component.Name));

    private BehaviorTree<MobContext> Build()
    {
        var branches = components.SelectMany(component => component.Branches()).OrderBy(branch => branch.Priority).Select(branch => branch.Node);
        var idle = components.SelectMany(component => component.Idle()).ToList();
        idle.Add(new Do<MobContext>("stoi", _ => NodeStatus.Running));
        var calm = new Sequence<MobContext>("spokój",
            new Do<MobContext>("uspokaja się", Calm),
            new Selector<MobContext>("zajęcie", [.. idle]));
        return new BehaviorTree<MobContext>(new Selector<MobContext>(Name, [.. branches, calm]));
    }

    private static NodeStatus Calm(MobContext c)
    {
        c.Mob.Target = null;
        if (c.Mob.WeaponOut && c.Now - c.Mob.LastCombat > TimeSpan.FromSeconds(4))
        {
            c.Mob.WeaponOut = false;
            if (c.Kind.Humanoid) c.Npc.RemoveWeapon();
        }
        foreach (MobComponent component in c.Behaviour.Components) component.Calm(c);
        return NodeStatus.Success;
    }
}
