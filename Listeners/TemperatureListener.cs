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
        private static readonly Regex FindRegex = new Regex(
            @"(?<=^|\s|[_*~])(-?\d*(?:\.\d+)?)\s?°?([FC])(?=$|\s|[_*~])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UrlRegex = new Regex(@"http[^\s]+", RegexOptions.Compiled);
        private readonly DiscordSocketClient _client;

        public TemperatureListener(DiscordSocketClient client)
        {
            _client = client;
        }

        public void Initialize()
        {
            _client.MessageReceived += TemperatureListenerAsync;
        }

        private Task TemperatureListenerAsync(SocketMessage socketMessage)
        {
            if (socketMessage.Author.IsBot) return Task.CompletedTask;
            
            Task.Run(async () =>
            {
                var self = await socketMessage.Channel.GetUserAsync(_client.CurrentUser.Id);
                if (self is IGuildUser gSelf)
                    if (gSelf.GetPermissions(socketMessage.Channel as IGuildChannel).SendMessages)
                        try
                        {
                            var list = new List<string>();
                            var content = UrlRegex.Replace(socketMessage.Content, "");
                            var matches = from m in FindRegex.Matches(content)
                                where m.Groups.Count == 3
                                select new
                                {
                                    Quantity = double.Parse(m.Groups[1].Value), Unit = m.Groups[2].Value.ToUpper()
                                };

                            foreach (var i in matches)
                                // ReSharper disable once SwitchStatementMissingSomeCases
                                switch (i.Unit)
                                {
                                    case "C":
                                        list.Add(
                                            $"{i.Quantity:#,##0.##} °C = {i.Quantity * 1.8 + 32.0:#,##0.##} °F");
                                        break;
                                    case "F":
                                        list.Add(
                                            $"{i.Quantity:#,##0.##} °F = {(i.Quantity - 32.0) / 1.8:#,##0.##} °C");
                                        break;
                                }

                            if (list.Any())
                                await socketMessage.Channel.SendMessageAsync(string.Join("\n", list));
                        }
                        catch (Exception)
                        {
                            // Do nothing
                        }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }
    }
}