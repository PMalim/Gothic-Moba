namespace BotArena.Ai.Behavior;

public sealed class BehaviorMemory
{
    private readonly object?[] slots;
    private readonly int[] running;
    private readonly IReadOnlyList<string> paths;

    internal BehaviorMemory(int nodeCount, IReadOnlyList<string> paths)
    {
        slots = new object?[nodeCount];
        running = Enumerable.Repeat(-1, nodeCount).ToArray();
        this.paths = paths;
    }

    internal T? Get<T>(int node) where T : class => slots[node] as T;
    internal void Set(int node, object? value) => slots[node] = value;
    internal ref int Running(int node) => ref running[node];

    internal void Activate<T>(Node<T> leaf) where T : IBehaviorContext
    {
        ActiveNode = leaf.Id;
        ActiveLeaf = leaf.Name;
    }

    public int ActiveNode { get; private set; } = -1;
    public string ActiveLeaf { get; private set; } = "";
    public string ActivePath => ActiveNode >= 0 ? paths[ActiveNode] : "";
    public string ActiveBranch => ActivePath.Split(Separator)[0];

    internal const string Separator = " › ";
}

public sealed class BehaviorTree<T> where T : IBehaviorContext
{
    private const string Separator = BehaviorMemory.Separator;
    private readonly Node<T> root;
    private readonly List<string> paths = [];
    public int NodeCount => paths.Count;
    public string Name => root.Name;

    public BehaviorTree(Node<T> root)
    {
        this.root = root;
        void Number(Node<T> node, string path)
        {
            if (node.Id >= 0) throw new InvalidOperationException($"Node '{node.Name}' is used twice in one tree.");
            node.Id = paths.Count;
            paths.Add(path);
            string inner = node == root || !node.IsBranch ? path : path.Length == 0 ? node.Name : path + Separator + node.Name;
            foreach (Node<T> child in node.Children) Number(child, inner);
        }
        Number(root, "");
    }

    public BehaviorMemory CreateMemory() => new(NodeCount, paths);
    public NodeStatus Tick(T context) => root.Tick(context);
    public void Abort(T context) => root.Abort(context);
}
