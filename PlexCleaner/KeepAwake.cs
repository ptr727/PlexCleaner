using System;
using System.Runtime.InteropServices;

namespace PlexCleaner;

public static class KeepAwake
{
    public static void PreventSleep()
    {
        // Windows only
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetThreadExecutionState(ExecutionState.EsContinuous | ExecutionState.EsSystemRequired);
    }

    public static void AllowSleep()
    {
        // Windows only
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            SetThreadExecutionState(ExecutionState.EsContinuous);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);

    [FlagsAttribute]
    private enum ExecutionState : uint
    {
        // ReSharper disable once UnusedMember.Local
        EsAwayModeRequired = 0x00000040,
        EsContinuous = 0x80000000,
        // ReSharper disable once UnusedMember.Local
        EsDisplayRequired = 0x00000002,
        EsSystemRequired = 0x00000001
    }
}