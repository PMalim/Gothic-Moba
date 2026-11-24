using GothicOnline.Network;

namespace BotArena.Lobby;

[Message(40)]
public sealed record UiPushMessage(string Name, string Json);

[Message(41)]
public sealed record UiActionMessage(string Name, string Json);

[Message(42)]
public sealed record ArenaPhaseMessage(int Phase, int Match);
