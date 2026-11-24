using GothicOnline.Commands;

namespace BotArena.Chat;

public static class ChatCommandList
{
    public static ChatCommandsMessage Build(Server server)
    {
        CommandInfo[] commands = [.. server.Commands.Commands.OrderBy(c => c.Permission is not null).ThenBy(c => c.Name, StringComparer.Ordinal)];
        return new ChatCommandsMessage(
            [.. commands.Select(c => c.Name)],
            [.. commands.Select(c => string.Join(',', c.Aliases))],
            [.. commands.Select(c => c.Description ?? "")],
            [.. commands.Select(c => c.Permission is null ? 0 : 1)]);
    }
}
