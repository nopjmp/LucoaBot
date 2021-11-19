using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;

namespace LucoaBot.Commands
{
    [RequireBotPermissions(Permissions.SendMessages)]
    public class SearchModule : BaseCommandModule
    {
        private readonly IHttpClientFactory _httpClientFactory;

        public SearchModule(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [Command("google")]
        [Description("Returns back the first result from Google.")]
        public async Task GoogleAsync(CommandContext context, [RemainingText] [Description("Text to search+")]
            string arg)
        {
            var query = HttpUtility.UrlEncode(arg);
            var httpClient = _httpClientFactory.CreateClient("noredirect");

            using var response = await httpClient.GetAsync($"https://www.google.com/search?q={query}&btnI");

            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location != null)
            {
                var uri = new Uri(response.Headers.Location.ToString());
                if (uri.Host.EndsWith("google.com"))
                {
                    var queryString = HttpUtility.ParseQueryString(uri.Query);
                    await context.RespondAsync(queryString.Get("q"));
                }
            }
            else
            {
                await context.RespondAsync("Google did not return a valid response.");
            }
        }
    }
}