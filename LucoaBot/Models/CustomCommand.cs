using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LucoaBot.Models
{
    public class CustomCommand
    {
        public int Id { get; set; }
        [Required]
        public ulong GuildId { get; set; }
        [Required]
        [StringLength(255)]
        public string Command { get; set; }
        [Required]
        [StringLength(2000)]
        public string Response { get; set; }
    }
}
