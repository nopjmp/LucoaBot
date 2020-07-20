using System;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
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
        private const string DiscordAppRaString = "https://discordapp.com/ra/";
        private const string DiscordRaString = "https://discord.com/ra/";
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

            _discordClient.MessageCreated += OnMessageReceived;
        }

        // This is more than what discord supports, but just to be careful.
        private static readonly string[] _validExtensions = {"jpg", "jpeg", "bmp", "png", "webp"};
        private static bool IsImageExtension(string filename)
        {
            var ext = Regex.Match(filename, @"\.[A-Za-z0-9]+$").Value;
            return _validExtensions.Contains(ext.ToLower());
        }
        
        private Task OnMessageReceived(MessageCreateEventArgs args)
        {
            if (args.Author.IsBot || args.Guild == null) return Task.CompletedTask;

            var attachments = args.Message.Attachments
                .Where(a => IsImageExtension(a.FileName))
                .Select(a => a.Url);
            
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
                catch (Exception e)
                {
                    _logger.LogError(e, "Error encountered within OnMessageReceived");
                }
            });

            Task.Run(async () => await Task.WhenAll(tasks));

            return Task.CompletedTask;
        }
    }
}