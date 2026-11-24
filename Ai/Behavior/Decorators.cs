namespace BotArena.Ai.Behavior;

public sealed class Cooldown<T>(TimeSpan period, Node<T> child) : Node<T>(child.Name) where T : IBehaviorContext
{
    private sealed class Ready(TimeSpan at) { public TimeSpan At { get; } = at; }
    internal override IReadOnlyList<Node<T>> Children => [child];
    internal override bool IsBranch => false;

    protected override NodeStatus Execute(T context)
    {
        if (context.Tree.Get<Ready>(Id) is { } ready && context.Now < ready.At) return NodeStatus.Failure;
        NodeStatus status = child.Tick(context);
        if (status != NodeStatus.Running) context.Tree.Set(Id, new Ready(context.Now + period));
        return status;
    }
}

public sealed class Optional<T>(Node<T> child) : Node<T>(child.Name) where T : IBehaviorContext
{
    internal override IReadOnlyList<Node<T>> Children => [child];
    internal override bool IsBranch => false;
    protected override NodeStatus Execute(T context) => child.Tick(context) == NodeStatus.Running ? NodeStatus.Running : NodeStatus.Success;
}
