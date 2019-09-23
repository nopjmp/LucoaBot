using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace LucoaBot.Commands
{
    [Name("Search")]
    [Group("search")]
    public class SearchModule : ModuleBase<SocketCommandContext>
    {
        private readonly IHttpClientFactory httpClientFactory;
        public SearchModule(IHttpClientFactory httpClientFactory)
        {
            this.httpClientFactory = httpClientFactory;
        }

        [Command("google")]
        [Summary("Returns back the first result from Google.")]
        public async Task<RuntimeResult> GoogleAsync([Remainder]string arg)
        {
            var query = HttpUtility.UrlEncode(arg);
            var httpClient = httpClientFactory.CreateClient("noredirect");

            var response = await httpClient.GetAsync($"https://www.google.com/search?q={query}&btnI");

            if (response.StatusCode == System.Net.HttpStatusCode.Redirect && response.Headers.Location != null)
                return CommandResult.FromSuccess(response.Headers.Location.ToString());
            else
                return CommandResult.FromError("Google did not return a valid response.");
        }
    }
}
