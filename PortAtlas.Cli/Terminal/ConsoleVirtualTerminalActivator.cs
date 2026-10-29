namespace PortAtlas.Cli.Terminal;

using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>
/// Configures and enables ANSI virtual terminal processing on Windows console hosts.
/// </summary>
public static class ConsoleVirtualTerminalActivator
{
    private const int StandardOutputHandle = -11;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    /// <summary>
    /// Attempts to enable ANSI/VT100 virtual terminal processing for the standard output stream.
    /// </summary>
    /// <returns>True if virtual terminal processing is active or natively supported, false if unsupported or redirected.</returns>
    public static bool TryEnable()
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        if (Console.IsOutputRedirected)
        {
            return false;
        }

        return EnableWindowsVirtualTerminal();
    }

    [SupportedOSPlatform("windows")]
    private static bool EnableWindowsVirtualTerminal()
    {
        var handle = GetStdHandle(StandardOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        if (!GetConsoleMode(handle, out var mode))
        {
            return false;
        }

        if ((mode & EnableVirtualTerminalProcessing) != 0)
        {
            return true;
        }

        var newMode = mode | EnableVirtualTerminalProcessing;
        return SetConsoleMode(handle, newMode);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}
