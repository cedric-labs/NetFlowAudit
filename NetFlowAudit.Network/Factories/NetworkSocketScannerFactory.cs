using NetFlowAudit.Core.Contracts;
using NetFlowAudit.Network.Linux;
using NetFlowAudit.Network.Windows;

namespace NetFlowAudit.Network.Factories;

/// <summary>
/// Fabrique instanciant le composant d'écoute adapté au système d'exploitation hôte.
/// </summary>
public static class NetworkSocketScannerFactory
{
    /// <summary>
    /// Crée l'instance appropriée de scanneur de sockets pour la plateforme courante.
    /// </summary>
    public static INetworkSocketScanner Create(IProcessInspector processInspector, IServerHostProvider hostProvider)
    {
        ArgumentNullException.ThrowIfNull(processInspector);
        ArgumentNullException.ThrowIfNull(hostProvider);

        if (OperatingSystem.IsWindows())
        {
            return new WindowsSocketScanner(processInspector, hostProvider);
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxProcSocketScanner(processInspector, hostProvider);
        }

        throw new PlatformNotSupportedException($"Le système d'exploitation {Environment.OSVersion.Platform} n'est pas pris en charge.");
    }
}
