using System;
using Serilog;

namespace PlexCleaner;

public static class Extensions
{
    public static bool LogAndPropagate(this ILogger logger, Exception exception, string function)
    {
        logger.Error(exception, "{Function}", function);
        return false;
    }

    public static bool LogAndHandle(this ILogger logger, Exception exception, string function)
    {
        logger.Error(exception, "{Function}", function);
        return true;
    }

    public class LogOverride { }

    public static ILogger LogOverrideContext(this ILogger logger) =>
        logger.ForContext<LogOverride>();
}
