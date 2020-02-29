using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using LucoaBot.Models;
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

        private readonly SimpleBus _bus;

        private readonly IHttpClientFactory _httpClientFactory;

        public QrCodeListener(ILogger<QrCodeListener> logger, SimpleBus bus,
            IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _bus = bus;
            _httpClientFactory = httpClientFactory;
        }

        public void Initialize()
        {
            _bus.MessageReceived += OnMessageReceived;
        }

        private Task OnMessageReceived(CustomContext context)
        {
            var attachments = context.Message.Attachments.Select(a => a.Url);
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
                    var reader = new BarcodeReader();
                    var result = reader.Decode(bitmap);
                    if (result != null)
                    {
                        if (result.Text.StartsWith(DiscordRaString))
                        {
                            _logger.LogInformation(
                                $"Found malicious login url qr code {result.BarcodeFormat} {result.Text} ");
                            await context.Message.DeleteAsync();

                            await _bus.SubmitLog(context.User, context.Guild,
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