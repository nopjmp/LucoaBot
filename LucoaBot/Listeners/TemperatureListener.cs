using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;

namespace LucoaBot.Listeners
{
    public class TemperatureListener
    {
        private static readonly Regex FindRegex = new Regex(
            @"(?<=^|\s|[_*~])(-?\d*(?:\.\d+)?)\s?°?([FC])(?=$|\s|[_*~])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UrlRegex = new Regex(@"http[^\s]+", RegexOptions.Compiled);

        private readonly DiscordClient _client;

        public TemperatureListener(DiscordClient client)
        {
            _client = client;

            _client.MessageCreated += TemperatureListenerAsync;
        }

        private Task TemperatureListenerAsync(MessageCreateEventArgs args)
        {
            Task.Run(async () =>
            {
                if (args.Author.IsBot) return;

                var permission = true;
                if (args.Guild != null) // check if we can send messages
                {
                    var self = await args.Guild.GetMemberAsync(_client.CurrentUser.Id);
                    permission = (self.PermissionsIn(args.Channel) & Permissions.SendMessages) != 0;
                }

                if (permission)
                    try
                    {
                        var list = new List<string>();
                        var content = UrlRegex.Replace(args.Message.Content, "");
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
                            await args.Channel.SendMessageAsync(string.Join("\n", list));
                    }
                    catch (Exception)
                    {
                        // Do nothing
                    }
            });
            return Task.CompletedTask;
        }
    }
}