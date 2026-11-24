using GothicOnline.Network;

namespace BotArena.WorldBuilder;

[Message(20)]
public sealed record BuilderPlaceMessage(string Visual, Position At, float Yaw, float Pitch, float Roll, float Scale, bool Collision);

[Message(21)]
public sealed record BuilderEditMessage(int Id, Position At, float Yaw, float Pitch, float Roll, float Scale, bool Collision);

[Message(22)]
public sealed record BuilderDeleteMessage(int Id);

[Message(24)]
public sealed record BuilderStateMessage(bool Enabled, string Storage);

[Message(25)]
public sealed record BuilderVobsMessage(bool Reset, int[] Ids, string[] Visuals, Position[] At, float[] Angles, float[] Scales, int[] Flags);

[Message(26)]
public sealed record BuilderGoneMessage(int Id);

[Message(27)]
public sealed record BuilderFilterMessage(string Text);
