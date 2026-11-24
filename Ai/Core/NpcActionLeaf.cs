namespace BotArena.Ai;

public sealed class NpcActionLeaf(string name, Func<MobContext, int> start, TimeSpan timeout,
    Action<MobContext>? started = null, Action<MobContext, bool>? finished = null) : Node<MobContext>(name)
{
    private sealed class InFlight(int action, TimeSpan since)
    {
        public int Action { get; } = action;
        public TimeSpan Since { get; } = since;
    }

    protected override NodeStatus Execute(MobContext context)
    {
        var flight = context.Tree.Get<InFlight>(Id);
        if (flight is null)
        {
            if (context.Mob.CurrentAction is not null) { context.Commands.Cancel(context.Npc); context.Mob.CurrentAction = null; }
            int action = start(context);
            if (action < 0) { Finish(context, false, TimeSpan.Zero); return NodeStatus.Failure; }
            context.Mob.CurrentAction = action;
            context.Tree.Set(Id, new InFlight(action, context.Now));
            context.Tree.Activate(this);
            started?.Invoke(context);
            return NodeStatus.Running;
        }
        if (context.Mob.TakeFinished(flight.Action, out bool success))
        {
            context.Tree.Set(Id, null);
            if (context.Mob.CurrentAction == flight.Action) context.Mob.CurrentAction = null;
            context.Mob.SetFocus(null, FocusKind.None);
            context.Replan = true;
            Finish(context, success, context.Now - flight.Since);
            return success ? NodeStatus.Success : NodeStatus.Failure;
        }
        if (context.Now - flight.Since > timeout)
        {
            context.Tree.Set(Id, null);
            context.Commands.Cancel(context.Npc);
            context.Mob.CurrentAction = null;
            context.Mob.SetFocus(null, FocusKind.None);
            Finish(context, false, context.Now - flight.Since);
            return NodeStatus.Failure;
        }
        context.Tree.Activate(this);
        return NodeStatus.Running;
    }

    private void Finish(MobContext context, bool success, TimeSpan elapsed)
    {
        context.ActionElapsed = elapsed;
        finished?.Invoke(context, success);
    }

    public override void Abort(MobContext context)
    {
        if (context.Tree.Get<InFlight>(Id) is not { } flight) return;
        context.Tree.Set(Id, null);
        if (context.Mob.CurrentAction != flight.Action) return;
        context.Commands.Cancel(context.Npc);
        context.Mob.CurrentAction = null;
        context.Mob.SetFocus(null, FocusKind.None);
    }
}
