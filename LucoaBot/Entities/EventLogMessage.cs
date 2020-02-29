namespace LucoaBot.Models
{
    public class EventLogMessage
    {
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
        public string Message { get; set; }
        public string ActionTaken { get; set; }
    }
}