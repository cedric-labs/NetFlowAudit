using System.Runtime.InteropServices;
using AwesomeAssertions;
using NetFlowAudit.Cli.Terminal;
using Xunit;

namespace NetFlowAudit.Tests.Terminal;

public sealed class ConsoleVirtualTerminalActivatorTests
{
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [Fact]
    public void TryEnable_WhenExecutedInCurrentEnvironment_ExecutesSafelyWithoutThrowing()
    {
        // Act
        var action = () => ConsoleVirtualTerminalActivator.TryEnable();

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void TryEnable_WhenConsoleIsAllocated_EnablesVirtualTerminalProcessingFlag()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        // Arrange: allocate a dedicated native console (conhost.exe)
        var allocated = AllocConsole();
        if (!allocated)
        {
            // If already attached to a console or in a restricted CI container, skip assertion
            return;
        }

        try
        {
            var hOut = GetStdHandle(-11);

            // Act
            var result = ConsoleVirtualTerminalActivator.TryEnable();

            // Assert
            result.Should().BeTrue();
            var success = GetConsoleMode(hOut, out var mode);
            success.Should().BeTrue();
            (mode & 0x0004).Should().Be(0x0004); // ENABLE_VIRTUAL_TERMINAL_PROCESSING
        }
        finally
        {
            FreeConsole();
        }
    }
}
