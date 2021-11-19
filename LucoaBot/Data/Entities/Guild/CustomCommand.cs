using System.ComponentModel.DataAnnotations;

namespace LucoaBot.Data.Entities
{
    public class CustomCommand
    {
        public int Id { get; set; }
        [Required] public ulong GuildId { get; set; }
        [Required] [StringLength(255)] public string Command { get; set; }
        [Required] [StringLength(2000)] public string Response { get; set; }
    }
}