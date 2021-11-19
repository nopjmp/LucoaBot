namespace LucoaBot.Data.Entities
{
    public struct UserActionMessage
    {
        public UserAction UserAction { get; set; }
        public ulong Id { get; set; }
        public ulong GuildId { get; set; }
        public string Username { get; set; }
        public string Discriminator { get; set; }
    }
}