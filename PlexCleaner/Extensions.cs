using Serilog;
using System;

namespace PlexCleaner
{
    public static class Extensions
    {
        public static bool LogAndPropagate(this ILogger logger, Exception exception, string message, params object[] args)
        {
            logger.Error(exception, message, args);
            return false;
        }

        public static bool LogAndHandle(this ILogger logger, Exception exception, string message, params object[] args)
        {
            logger.Error(exception, message, args);
            return true;
        }
    }
}
