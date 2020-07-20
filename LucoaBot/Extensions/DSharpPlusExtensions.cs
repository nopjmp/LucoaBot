using System;
using Microsoft.Extensions.Logging;

namespace LucoaBot
{
    public static class DSharpPlusExtensions
    {
        public static LogLevel AsLoggingLevel(this DSharpPlus.LogLevel logLevel)
        {
            return logLevel switch
            {
                DSharpPlus.LogLevel.Debug => LogLevel.Debug,
                DSharpPlus.LogLevel.Info => LogLevel.Information,
                DSharpPlus.LogLevel.Warning => LogLevel.Warning,
                DSharpPlus.LogLevel.Error => LogLevel.Error,
                DSharpPlus.LogLevel.Critical => LogLevel.Critical,
                _ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
            };
        }
    }
}