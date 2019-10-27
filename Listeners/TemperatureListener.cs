using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace LucoaBot.Listeners
{
    public class TemperatureListener
    {
        private readonly DiscordSocketClient _client;

        private static readonly Regex FindRegex = new Regex(@"(?<=^|\s|[_*~])(-?\d*(?:\.\d+)?)\s?°?([FC])(?=$|\s|[_*~])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex UrlRegex = new Regex(@"http[^\s]+", RegexOptions.Compiled);

        public TemperatureListener(DiscordSocketClient client)
        {
            _client = client;
        }

        public void Initialize()
        {
            _client.MessageReceived += TemperatureListenerAsync;
        }

        private Task TemperatureListenerAsync(SocketMessage m)
        {
            if (!m.Author.IsBot)
            {
                Task.Run(async () =>
                {
                    var self = await m.Channel.GetUserAsync(_client.CurrentUser.Id);
                    if (self is IGuildUser gSelf)
                    {
                        if (gSelf.GetPermissions(m.Channel as IGuildChannel).SendMessages)
                        {
                            try
                            {
                                var list = new List<string>();
                                var content = UrlRegex.Replace(m.Content, "");
                                var matches = from m in FindRegex.Matches(content)
                                              where m.Groups.Count == 3
                                              select new { Quantity = double.Parse(m.Groups[1].Value), Unit = m.Groups[2].Value.ToUpper() };

                                foreach (var item in matches)
                                {
                                    // ReSharper disable once SwitchStatementMissingSomeCases
                                    switch (item.Unit)
                                    {
                                        case "C":
                                            list.Add(
                                                $"{item.Quantity:#,##0.##} °C = {(item.Quantity * 1.8) + 32.0:#,##0.##} °F");
                                            break;
                                        case "F":
                                            list.Add(
                                                $"{item.Quantity:#,##0.##} °F = {(item.Quantity - 32.0) / 1.8:#,##0.##} °C");
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
                }).SafeFireAndForget(false);
            }

            return Task.CompletedTask;
        }
    }
}
