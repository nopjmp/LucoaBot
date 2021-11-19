using System.ComponentModel.DataAnnotations;

namespace LucoaBot.Data
{
    public class StarboardCache
    {
        public int Id { get; set; }
        public ulong StarboardId { get; set; }
        public ulong MessageId { get; set; }
        public ulong GuildId { get; set; }
    }
}