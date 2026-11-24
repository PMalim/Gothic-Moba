using GothicOnline.Network;

namespace BotArena;

[Message(1)]
public sealed record ArenaStatusMessage(int AliveBots, int TotalBots);

[Message(2)]
public sealed record ArenaKillMessage(string Victim, int Kills);

[Message(55)]
public sealed record ArenaGoldMessage(Position At, int Amount);

[Message(60)]
public sealed record ArenaDamageMessage(Position At, int Amount, int Type, bool Taken);

[Message(56)]
public sealed record ArenaNavViewMessage(float OriginX, float OriginZ, float Cell, int Width, float BaseY, string Cells);

[Message(3)]
public sealed record ArenaDebugModeMessage(bool Enabled);

[Message(4)]
public sealed record ArenaMobDebugMessage(
    int Npc, string Name, string State, string Branch, string Leaf, int Target, string TargetName,
    Position Home, Position Focus, int FocusKind, float SightRange, float AttackRange, int Health, int MaxHealth,
    int FlankAngle, int QueuedActions, string Note,
    string Behaviour, string Components, float PursuitRange, float LeashRange,
    int[] Known, Position[] KnownAt, int[] KnownFlags, int[] KnownAge);

[Message(5)]
public sealed record ArenaTurretMessage(int Npc, string Name, Position At, int Team, int MaxHealth);

[Message(6)]
public sealed record ArenaTurretGoneMessage(int Npc);

[Message(7)]
public sealed record ArenaTurretShotMessage(int Turret, int Target);

[Message(9)]
public sealed record ArenaAdminStateMessage(bool Admin);

[Message(10)]
public sealed record ArenaAdminPlayersMessage(int[] Ids, string[] Names, int[] Health, int[] MaxHealth, int[] Ping, int[] Flags, int Minions);

[Message(11)]
public sealed record ArenaAdminActionMessage(int Action, int Target, float X, float Y, float Z);

[Message(12)]
public sealed record ArenaGhostMessage(int Player, bool On);

[Message(13)]
public sealed record ArenaFreezeMessage(bool On);

[Message(14)]
public sealed record ArenaAdminUiMessage(int Command);

[Message(15)]
public sealed record ArenaDamageReportMessage(int Attacker, int Victim, int Damage);

[Message(16)]
public sealed record ArenaMinionShotMessage(int Npc, int Target, int Team, bool Heavy);

[Message(75)]
public sealed record ArenaMobAniMessage(int Npc, string Ani);

[Message(30)]
public sealed record ChatCommandsMessage(string[] Names, string[] Aliases, string[] Descriptions, int[] Admin);
