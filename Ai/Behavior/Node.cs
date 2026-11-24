namespace BotArena.Ai.Behavior;

public enum NodeStatus { Success, Failure, Running }

public interface IBehaviorContext
{
    BehaviorMemory Tree { get; }
    TimeSpan Now { get; }
}

public abstract class Node<T>(string name) where T : IBehaviorContext
{
    public string Name { get; } = name;
    internal int Id { get; set; } = -1;
    internal virtual IReadOnlyList<Node<T>> Children => [];
    internal virtual bool IsBranch => Children.Count > 1;

    public NodeStatus Tick(T context) => Execute(context);
    protected abstract NodeStatus Execute(T context);

    public virtual void Abort(T context)
    {
        foreach (Node<T> child in Children) child.Abort(context);
    }
}
