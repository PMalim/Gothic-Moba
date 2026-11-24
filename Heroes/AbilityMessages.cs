using GothicOnline.Network;

namespace BotArena.Heroes;

[Message(43)]
public sealed record AbilityCastMessage(int Slot, float DirX, float DirZ);

[Message(44)]
public sealed record AbilityFxMessage(int Caster, int Target, int Kind, string Fx);

[Message(45)]
public sealed record AbilityDashMessage(Position To);

[Message(46)]
public sealed record AbilityAuraMessage(int Caster, string Fx, int Millis);

[Message(47)]
public sealed record AbilityGroundMessage(int Caster, string Fx, Position At, int Millis);

[Message(48)]
public sealed record AbilitySpeedMessage(float Scale, int Millis);

[Message(49)]
public sealed record AbilityRollMessage(Position To, int Millis);

[Message(50)]
public sealed record AbilityAnimMessage(string Animation);

[Message(51)]
public sealed record AbilityAimMessage(int Slot, float Range, float Radius, string Visual);

[Message(52)]
public sealed record AbilityThrowMessage(int Slot, Position At);

[Message(53)]
public sealed record AbilityBarrelMessage(int Caster, Position From, Position To, int Millis, string Fx);

[Message(54)]
public sealed record AbilityMotionMessage(string Animation, int Millis, int Mode, Position To);

[Message(57)]
public sealed record ArenaStasisMessage(int Player, bool On);

[Message(58)]
public sealed record AbilityRankMessage(int Slot);

[Message(61)]
public sealed record AbilitySwordMessage(int Caster, Position At, int Millis, string Visual, string Fx);

[Message(62)]
public sealed record AbilityTotemMessage(int Caster, Position At, int Millis, string Visual, string Fx);

[Message(63)]
public sealed record AbilitySkullMessage(int Target, int Millis, string Visual);
