using System.ComponentModel.DataAnnotations;

namespace LucoaBot.Models
{
    public class GuildConfig
    {
        public int Id { get; set; }
        [Required]
        public ulong GuildId { get; set; }
        [Required]
        [StringLength(16)]
        public string Prefix { get; set; }
        public ulong? LogChannel { get; set; }
        public ulong? StarBoardChannel { get; set; }
    }
}
