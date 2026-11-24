namespace BotArena.Ai.Behavior;

public sealed class Selector<T>(string name, params Node<T>[] children) : Node<T>(name) where T : IBehaviorContext
{
    internal override IReadOnlyList<Node<T>> Children => children;
    internal override bool IsBranch => true;

    protected override NodeStatus Execute(T context)
    {
        ref int running = ref context.Tree.Running(Id);
        for (int i = 0; i < children.Length; i++)
        {
            NodeStatus status = children[i].Tick(context);
            if (status == NodeStatus.Failure) continue;
            if (running > i) children[running].Abort(context);
            running = status == NodeStatus.Running ? i : -1;
            return status;
        }
        running = -1;
        return NodeStatus.Failure;
    }

    public override void Abort(T context)
    {
        ref int running = ref context.Tree.Running(Id);
        if (running >= 0) children[running].Abort(context);
        running = -1;
    }
}

public sealed class Sequence<T>(string name, params Node<T>[] children) : Node<T>(name) where T : IBehaviorContext
{
    internal override IReadOnlyList<Node<T>> Children => children;
    internal override bool IsBranch => true;

    protected override NodeStatus Execute(T context)
    {
        ref int running = ref context.Tree.Running(Id);
        for (int i = 0; i < children.Length; i++)
        {
            NodeStatus status = children[i].Tick(context);
            if (status == NodeStatus.Success) continue;
            if (running > i) children[running].Abort(context);
            running = status == NodeStatus.Running ? i : -1;
            return status;
        }
        running = -1;
        return NodeStatus.Success;
    }

    public override void Abort(T context)
    {
        ref int running = ref context.Tree.Running(Id);
        if (running >= 0) children[running].Abort(context);
        running = -1;
    }
}

public sealed class Steps<T>(string name, params Node<T>[] children) : Node<T>(name) where T : IBehaviorContext
{
    internal override IReadOnlyList<Node<T>> Children => children;
    internal override bool IsBranch => true;

    protected override NodeStatus Execute(T context)
    {
        ref int step = ref context.Tree.Running(Id);
        for (int i = Math.Max(step, 0); i < children.Length; i++)
        {
            NodeStatus status = children[i].Tick(context);
            if (status == NodeStatus.Success) continue;
            step = status == NodeStatus.Running ? i : -1;
            return status;
        }
        step = -1;
        return NodeStatus.Success;
    }

    public override void Abort(T context)
    {
        ref int step = ref context.Tree.Running(Id);
        if (step >= 0) children[step].Abort(context);
        step = -1;
    }
}
