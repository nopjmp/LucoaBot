using Discord.Commands;

namespace LucoaBot
{
    public class CommandResult : RuntimeResult
    {
        private CommandResult(CommandError? error, string reason) : base(error, reason)
        {
        }

        public static CommandResult FromError(string reason)
        {
            return new CommandResult(CommandError.Unsuccessful, reason);
        }

        public static CommandResult FromSuccess(string reason)
        {
            return new CommandResult(null, reason);
        }
    }
}