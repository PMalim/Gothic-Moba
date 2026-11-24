namespace BotArena.Ai.Behavior;

public sealed class Condition<T>(string name, Func<T, bool> test) : Node<T>(name) where T : IBehaviorContext
{
    protected override NodeStatus Execute(T context) => test(context) ? NodeStatus.Success : NodeStatus.Failure;
}

public sealed class Do<T>(string name, Func<T, NodeStatus> action) : Node<T>(name) where T : IBehaviorContext
{
    protected override NodeStatus Execute(T context)
    {
        NodeStatus status = action(context);
        if (status != NodeStatus.Failure) context.Tree.Activate(this);
        return status;
    }
}

public sealed class Wait<T>(string name, Func<T, TimeSpan> duration, Action<T>? started = null) : Node<T>(name) where T : IBehaviorContext
{
    private sealed class Until(TimeSpan value) { public TimeSpan Value { get; } = value; }

    protected override NodeStatus Execute(T context)
    {
        var until = context.Tree.Get<Until>(Id);
        if (until is null)
        {
            context.Tree.Set(Id, new Until(context.Now + duration(context)));
            started?.Invoke(context);
            context.Tree.Activate(this);
            return NodeStatus.Running;
        }
        if (context.Now < until.Value) { context.Tree.Activate(this); return NodeStatus.Running; }
        context.Tree.Set(Id, null);
        return NodeStatus.Success;
    }

    public override void Abort(T context) => context.Tree.Set(Id, null);
}
