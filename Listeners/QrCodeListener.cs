using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using LucoaBot.Services;
using Microsoft.Extensions.Logging;
using ZXing.ImageSharp;

namespace LucoaBot.Listeners
{
    public class QrCodeListener
    {
        private const string DiscordAppRaString = "https://discordapp.com/ra/";
        private const string DiscordRaString = "https://discord.com/ra/";

        // This is more than what discord supports, but just to be careful.
        private static readonly string[] _validExtensions = {".jpg", ".jpeg", ".bmp", ".png", ".webp"};
        private readonly BusQueue _busQueue;
        private readonly DiscordClient _discordClient;
        private readonly IHttpClientFactory _httpClientFactory;

        private readonly ILogger<QrCodeListener> _logger;

        public QrCodeListener(ILogger<QrCodeListener> logger,
            IHttpClientFactory httpClientFactory, DiscordClient discordClient, BusQueue busQueue)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _discordClient = discordClient;
            _busQueue = busQueue;

            _discordClient.MessageCreated += (_, e) =>
            {
                OnMessageReceived(e).Forget();
                return Task.CompletedTask;
            };
        }

        private static bool IsImageExtension(string filename)
        {
            var ext = Regex.Match(filename, @"\.[A-Za-z0-9]+$").Value;
            return _validExtensions.Contains(ext.ToLower());
        }

        private async Task OnMessageReceived(MessageCreateEventArgs args)
        {
            if (args.Author.IsBot || args.Guild == null) return;

            var attachments = args.Message.Attachments
                .Where(a => IsImageExtension(a.FileName))
                .Select(a => a.Url);

            var embeds = args.Message.Embeds
                .Where(a => a.Url.IsAbsoluteUri && IsImageExtension(a.Url.Segments.Last()))
                .Select(a => a.Url.ToString());

            var urls = Enumerable.Empty<string>().Concat(attachments).Concat(embeds);

            var tasks = urls.Select(async url =>
            {
                var httpClient = _httpClientFactory.CreateClient();
                await using var response = await httpClient.GetStreamAsync(url);

                try
                {
                    using (var image = SixLabors.ImageSharp.Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(response))
                    {
                        var reader = new BarcodeReader<SixLabors.ImageSharp.PixelFormats.Rgba32>();
                        var result = reader.Decode(image);
                        if (result != null)
                            if (result.Text.StartsWith(DiscordRaString) || result.Text.StartsWith(DiscordAppRaString))
                            {
                                _logger.LogInformation(
                                    $"Found malicious login url qr code {result.BarcodeFormat} {result.Text} ");
                                await args.Message.DeleteAsync();

                                await _busQueue.SubmitLog(args.Author, args.Guild,
                                    $"sent malicious login url qr code `{result.BarcodeFormat}` `{result.Text}`",
                                    "deleted message");
                            }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Error encountered within OnMessageReceived");
                }
            });

            await Task.WhenAll(tasks);
        }
    }
}