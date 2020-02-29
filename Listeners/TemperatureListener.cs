using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using LucoaBot.Models;
using LucoaBot.Services;

namespace LucoaBot.Listeners
{
    public class TemperatureListener
    {
        private static readonly Regex FindRegex = new Regex(
            @"(?<=^|\s|[_*~])(-?\d*(?:\.\d+)?)\s?°?([FC])(?=$|\s|[_*~])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UrlRegex = new Regex(@"http[^\s]+", RegexOptions.Compiled);

        private readonly DiscordSocketClient _client;
        private readonly SimpleBus _bus;

        public TemperatureListener(DiscordSocketClient client, SimpleBus bus)
        {
            _client = client;
            _bus = bus;
        }

        public void Initialize()
        {
            _bus.MessageReceived += TemperatureListenerAsync;
        }

        private async Task TemperatureListenerAsync(CustomContext context)
        {
            var self = await context.Channel.GetUserAsync(_client.CurrentUser.Id);
            if (self is IGuildUser gSelf)
            {
                if (gSelf.GetPermissions(context.Channel as IGuildChannel).SendMessages)
                    try
                    {
                        var list = new List<string>();
                        var content = UrlRegex.Replace(context.Message.Content, "");
                        var matches = from m in FindRegex.Matches(content)
                            where m.Groups.Count == 3
                            select (double.Parse(m.Groups[1].Value), m.Groups[2].Value.ToUpper());

                        foreach (var (temp, unit) in matches)
                            // ReSharper disable once SwitchStatementMissingSomeCases
                            switch (unit)
                            {
                                case "C":
                                    list.Add($"{temp:#,##0.##} °C = {temp * 1.8 + 32.0:#,##0.##} °F");
                                    break;
                                case "F":
                                    list.Add($"{temp:#,##0.##} °F = {(temp - 32.0) / 1.8:#,##0.##} °C");
                                    break;
                            }

                        if (list.Any())
                            await context.Channel.SendMessageAsync(string.Join("\n", list));
                    }
                    catch (Exception)
                    {
                        // Do nothing
                    }
            }
        }
    }
}