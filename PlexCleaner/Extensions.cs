using System;
using Serilog;

namespace PlexCleaner;

public static class Extensions
{
    extension(ILogger logger)
    {
        public bool LogAndPropagate(
            Exception exception,
            [System.Runtime.CompilerServices.CallerMemberName] string function = "unknown"
        )
        {
            logger.Error(exception, "{Function}", function);
            return false;
        }

        public bool LogAndHandle(
            Exception exception,
            [System.Runtime.CompilerServices.CallerMemberName] string function = "unknown"
        )
        {
            logger.Error(exception, "{Function}", function);
            return true;
        }

        public ILogger LogOverrideContext() => logger.ForContext<LogOverride>();
    }

    public class LogOverride;
}
