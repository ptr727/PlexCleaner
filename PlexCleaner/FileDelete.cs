using System.Runtime.CompilerServices;
using Serilog.Events;

namespace PlexCleaner;

internal static class FileDelete
{
    public static bool TryDelete(
        LogEventLevel level,
        string what,
        string reason,
        string fileName,
        Action<string>? delete = null,
        [CallerMemberName] string function = "unknown"
    )
    {
        try
        {
            Delete(level, what, reason, fileName, delete);
        }
        catch (Exception e) when (Log.Logger.LogAndHandle(e, function))
        {
            return false;
        }
        return true;
    }

    public static void Delete(
        LogEventLevel level,
        string what,
        string reason,
        string fileName,
        Action<string>? delete = null
    )
    {
        Log.Write(level, "Deleting {What:l} : {Reason:l} : {FileName}", what, reason, fileName);
        try
        {
            (delete ?? File.Delete)(fileName);
        }
        catch
        {
            Log.Error("Failed to delete {What:l} : {FileName}", what, fileName);
            throw;
        }
    }
}
