using GothicOnline.Network;

namespace BotArena.Vitals;

[Message(31)]
public sealed record ArenaRosterMessage(int[] Ids, int[] Kinds, string[] Names, int[] Teams, int[] Locked);

[Message(32)]
public sealed record ArenaVitalsMessage(int[] Ids, int[] Health, int[] MaxHealth, Position[] At);
