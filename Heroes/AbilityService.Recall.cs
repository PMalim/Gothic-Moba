using BotArena.GameData;
using BotArena.Matches;
using GothicOnline.Api;

namespace BotArena.Heroes;

public sealed partial class AbilityService
{
    public const int RecallSlot = 4;
    public static readonly TimeSpan RecallTime = TimeSpan.FromSeconds(5);

    public const string RecallFx = "SPELLFX_TELEPORT";

    public const float BaseHealPerSecond = 0.10f;

    public const float BaseRange = 1800f;

    private readonly Dictionary<PlayerId, (TimeSpan Due, int Health)> recalls = [];

    private void StartRecall(Player player)
    {
        if (recalls.ContainsKey(player.Id)) { Notice?.Invoke(player, "Powrót do bazy już trwa."); return; }
        if (InStasis(player)) { Notice?.Invoke(player, "W stasis nic nie zrobisz."); return; }
        if (matches.Of(player) is not { } match) return;
        recalls[player.Id] = (server.Clock.Elapsed + RecallTime, player.Health);
        matches.Send(match, new AbilityAuraMessage(MobCommands.IdOf(player), RecallFx, (int)RecallTime.TotalMilliseconds));
        Notice?.Invoke(player, "Powrót do bazy: 5 s. Obrażenia go przerywają.");
    }

    private bool CancelRecall(Player player, string? why = null)
    {
        if (!recalls.Remove(player.Id)) return false;
        if (player.IsValid && matches.Of(player) is { } match) matches.Send(match, new AbilityAuraMessage(MobCommands.IdOf(player), RecallFx, 0));
        if (why is not null && player.IsValid) Notice?.Invoke(player, why);
        return true;
    }

    private void UpdateRecalls(TimeSpan now)
    {
        foreach (PlayerId id in recalls.Keys.ToArray())
        {
            if (server.Players.Find(id) is not { IsValid: true } player || player.IsDead || matches.Of(player) is not { State: MatchState.Running }) { recalls.Remove(id); continue; }
            (TimeSpan due, int health) = recalls[id];
            if (player.Health < health) { CancelRecall(player, "Powrót do bazy przerwany."); continue; }
            recalls[id] = (due, player.Health);
            if (now < due) continue;
            recalls.Remove(id);
            matches.TeleportToBase(player);
            if (matches.Of(player) is { } match) matches.Send(match, new AbilityFxMessage(MobCommands.IdOf(player), -1, 1, RecallFx));
        }
    }

    private bool AtBase(Player player) =>
        matches.Of(player)?.Players.GetValueOrDefault(player.Id) is { } mp && MatchService.BaseOf(mp) is { } home && player.Position.DistanceTo(home).Centimeters <= BaseRange;
}
