using GothicOnline.Network;

namespace BotArena.Waypoints;

[Message(59)]
public sealed record ArenaWaypointsMessage(int Selected, int[] Ids, string[] Names, float[] X, float[] Y, float[] Z, int[] LinkA, int[] LinkB);
