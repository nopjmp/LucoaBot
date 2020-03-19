using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.EventArgs;
using LucoaBot.Services;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using ZXing.SkiaSharp;

namespace LucoaBot.Listeners
{
    public class QrCodeListener
    {
        private const string DiscordRaString = "https://discordapp.com/ra/";
        private readonly ILogger<QrCodeListener> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly DiscordClient _discordClient;
        private readonly BusQueue _busQueue;

        public QrCodeListener(ILogger<QrCodeListener> logger,
            IHttpClientFactory httpClientFactory, DiscordClient discordClient, BusQueue busQueue)
        {
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _discordClient = discordClient;
            _busQueue = busQueue;

            _discordClient.MessageCreated += OnMessageReceived;
        }

        private Task OnMessageReceived(MessageCreateEventArgs args)
        {
            if (args.Author.IsBot) return Task.CompletedTask;
            
            var attachments = args.Message.Attachments.Select(a => a.Url);
            // XXX: we might need to support embeds if it gets real bad...
            // var images = context.Message.Embeds
            //     .Where(e => e.Image != null)
            //     .Select(e => e.Image?.Url)
            var tasks = attachments.Select(async url =>
            {
                var httpClient = _httpClientFactory.CreateClient();
                await using var response = await httpClient.GetStreamAsync(url);

                try
                {
                    using var bitmap = SKBitmap.Decode(response);
                    if (bitmap == null)
                        return;
                    
                    var reader = new BarcodeReader();
                    var result = reader.Decode(bitmap);
                    if (result != null)
                    {
                        if (result.Text.StartsWith(DiscordRaString))
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

            return Task.WhenAll(tasks);
        }
    }
}