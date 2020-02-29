using Discord;

namespace LucoaBot.Models
{
    public class ReactionMessage
    {
        public ReactionAction ReactionAction { get; set; }
        public ulong MessageId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong GuildId { get; set; }
        public string Emote { get; set; }
    }
}