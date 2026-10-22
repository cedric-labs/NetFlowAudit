using System.Net;

namespace NetFlowAudit.Core.Models;

/// <summary>
/// Point de terminaison réseau typé et immuable encapsulant une adresse IP et un numéro de port.
/// </summary>
public readonly record struct NetworkEndpoint
{
    /// <summary>
    /// Initialise une nouvelle instance du point de terminaison réseau.
    /// </summary>
    /// <param name="address">Adresse IP.</param>
    /// <param name="port">Numéro de port (0 à 65535).</param>
    public NetworkEndpoint(IPAddress address, int port)
    {
        ArgumentNullException.ThrowIfNull(address);
        if (port is < 0 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(port), port, "Le port réseau doit être compris entre 0 et 65535.");
        }

        Address = address;
        Port = port;
    }

    /// <summary>
    /// Adresse IP du point de terminaison.
    /// </summary>
    public IPAddress Address { get; }

    /// <summary>
    /// Numéro de port.
    /// </summary>
    public int Port { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
            ? $"[{Address}]:{Port}"
            : $"{Address}:{Port}";
    }
}
