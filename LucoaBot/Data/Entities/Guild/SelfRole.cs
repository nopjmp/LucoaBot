using System.ComponentModel.DataAnnotations;

namespace LucoaBot.Data.Entities
{
    public class SelfRole
    {
        public int Id { get; set; }
        public ulong GuildId { get; set; }
        [StringLength(255)] public string Category { get; set; }
        public ulong RoleId { get; set; }
    }
}