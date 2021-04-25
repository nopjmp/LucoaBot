using DSharpPlus.Entities;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucoaBot.Extensions
{
    public static partial class DiscordEmojiExtension
    {
        public static string GetEmojiURL(this DiscordEmoji emoji)
        {
            if (emoji.Id == 0)
            {
                if (Utils.AssetFileNames.TryGetValue(emoji.Name, out var filename))
                    return Utils.AssetBaseURL + filename;
            }
            return emoji.Url;
        }
    }


}
