using System.ComponentModel.DataAnnotations;

namespace LucoaBot.Models
{
    public class StarboardCache
    {
        public int Id { get; set; }
        public ulong StarboardId { get; set; }
        public ulong MessageId { get; set; }
        public ulong GuildId { get; set; }
    }
}