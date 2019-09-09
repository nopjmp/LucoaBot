using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LucoaBot.Listeners
{
    public class TemperatureListener
    {
        private readonly DiscordSocketClient client;

        private readonly Regex findRegex = new Regex(@"(?<=^|\s|[_*~])(-?\d*(?:\.\d+)?)\s?°?([FC])(?=$|\s|[_*~])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public TemperatureListener(DiscordSocketClient client)
        {
            this.client = client;
        }

        public void Initialize()
        {
            client.MessageReceived += TemperatureListenerAsync;
        }

        public Task TemperatureListenerAsync(SocketMessage m)
        {
            if (!m.Author.IsBot)
            {
                var _ = Task.Run(async () =>
                {
                    var self = await m.Channel.GetUserAsync(client.CurrentUser.Id);
                    if (self is IGuildUser gSelf)
                    {
                        if (gSelf.GetPermissions(m.Channel as IGuildChannel).SendMessages)
                        {
                            try
                            {
                                var list = new List<string>();
                                var matches = from m in findRegex.Matches(m.Content)
                                              where m.Groups.Count == 3
                                              select new { Quantity = double.Parse(m.Groups[1].Value), Unit = m.Groups[2].Value.ToUpper() };

                                foreach (var item in matches)
                                {
                                    switch (item.Unit)
                                    {
                                        case "C":
                                            list.Add(string.Format("{0:#,##0.##} °C = {1:#,##0.##} °F", item.Quantity, (item.Quantity * 1.8) + 32.0));
                                            break;
                                        case "F":
                                            list.Add(string.Format("{0:#,##0.##} °F = {1:#,##0.##} °C", item.Quantity, (item.Quantity - 32.0) / 1.8));
                                            break;
                                    }
                                }

                                if (list.Any())
                                    await m.Channel.SendMessageAsync(string.Join("\n", list));
                            }
                            catch (Exception)
                            {
                                // Do nothing
                            }
                        }
                    }
                });
            }

            return Task.CompletedTask;
        }
    }
}
