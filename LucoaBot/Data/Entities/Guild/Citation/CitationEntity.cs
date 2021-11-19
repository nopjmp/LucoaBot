namespace LucoaBot.Data.Entities
{
    public class CitationEntity
    {
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public int CitationNumber { get; set; }
        
    }
}