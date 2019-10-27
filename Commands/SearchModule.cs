using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using Discord.Commands;

namespace LucoaBot.Commands
{
    [Name("Search")]
    [Group("search")]
    public class SearchModule : ModuleBase<SocketCommandContext>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        public SearchModule(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [Command("google")]
        [Summary("Returns back the first result from Google.")]
        public async Task<RuntimeResult> GoogleAsync([Remainder]string arg)
        {
            var query = HttpUtility.UrlEncode(arg);
            var httpClient = _httpClientFactory.CreateClient("noredirect");

            var response = await httpClient.GetAsync($"https://www.google.com/search?q={query}&btnI");

            if (response.StatusCode == HttpStatusCode.Redirect && response.Headers.Location != null)
                return CommandResult.FromSuccess(response.Headers.Location.ToString());
            return CommandResult.FromError("Google did not return a valid response.");
        }
    }
}
