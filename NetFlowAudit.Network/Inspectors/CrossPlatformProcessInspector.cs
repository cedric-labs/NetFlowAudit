using System.Diagnostics;
using System.Text.RegularExpressions;
using NetFlowAudit.Core.Contracts;
using NetFlowAudit.Core.Models;

namespace NetFlowAudit.Network.Inspectors;

/// <summary>
/// Résolveur de processus multiplateforme capable d'extraire la cible réelle (Java, cmd, scripts, etc.).
/// </summary>
public sealed class CrossPlatformProcessInspector : IProcessInspector
{
    private static readonly Regex JarPattern = new(@"\s+(-jar\s+|(?<=\.jar\s+)|^)(?<target>[^\s""']+\.jar)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromMilliseconds(500));
    private static readonly Regex ScriptPattern = new(@"(?<target>[^\s""']+\.(bat|cmd|ps1|sh|py))", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture, TimeSpan.FromMilliseconds(500));

    /// <inheritdoc />
    public Task<ProcessMetadata> ResolveProcessAsync(int processId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (processId <= 0)
        {
            return Task.FromResult(new ProcessMetadata(
                ProcessId: processId,
                ProcessName: processId == 0 ? "System Idle" : "Unknown",
                ExecutablePath: string.Empty,
                CommandLine: string.Empty,
                TargetExecutable: string.Empty,
                UserName: "SYSTEM"));
        }

        try
        {
            using var proc = Process.GetProcessById(processId);
            var processName = proc.ProcessName;
            var executablePath = TryGetExecutablePath(proc);
            var commandLine = TryGetCommandLine(proc);
            var targetPayload = ResolveTargetPayload(processName, commandLine);

            return Task.FromResult(new ProcessMetadata(
                ProcessId: processId,
                ProcessName: processName,
                ExecutablePath: executablePath,
                CommandLine: commandLine,
                TargetExecutable: targetPayload,
                UserName: string.Empty));
        }
        catch (ArgumentException)
        {
            // Le processus s'est terminé entre la capture de socket et l'inspection
            return Task.FromResult(new ProcessMetadata(
                ProcessId: processId,
                ProcessName: "Terminated",
                ExecutablePath: string.Empty,
                CommandLine: string.Empty,
                TargetExecutable: string.Empty,
                UserName: string.Empty));
        }
    }

    private static string TryGetExecutablePath(Process proc)
    {
        try
        {
            if (OperatingSystem.IsWindows() || OperatingSystem.IsLinux())
            {
                var module = proc.MainModule;
                return module?.FileName ?? string.Empty;
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // Accès refusé pour les processus privilégiés (ex: services système)
        }

        return string.Empty;
    }

    private static string TryGetCommandLine(Process proc)
    {
        if (OperatingSystem.IsLinux())
        {
            var cmdlinePath = $"/proc/{proc.Id}/cmdline";
            if (File.Exists(cmdlinePath))
            {
                try
                {
                    var bytes = File.ReadAllBytes(cmdlinePath);
                    return System.Text.Encoding.UTF8.GetString(bytes).Replace('\0', ' ').Trim();
                }
                catch (IOException)
                {
                    // Erreur lecture I/O /proc
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Analyse la ligne de commande pour extraire le fichier cible réel (JAR exécuté, script .bat/.cmd/.ps1).
    /// </summary>
    public static string ResolveTargetPayload(string processName, string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return string.Empty;
        }

        var isJava = processName.Contains("java", StringComparison.OrdinalIgnoreCase);
        if (isJava)
        {
            var match = JarPattern.Match(commandLine);
            if (match.Success)
            {
                return match.Groups["target"].Value;
            }
        }

        var isScriptHost = processName.Equals("cmd", StringComparison.OrdinalIgnoreCase) ||
                           processName.Equals("powershell", StringComparison.OrdinalIgnoreCase) ||
                           processName.Equals("pwsh", StringComparison.OrdinalIgnoreCase) ||
                           processName.Equals("bash", StringComparison.OrdinalIgnoreCase) ||
                           processName.Equals("sh", StringComparison.OrdinalIgnoreCase);

        if (isScriptHost)
        {
            var match = ScriptPattern.Match(commandLine);
            if (match.Success)
            {
                return match.Groups["target"].Value;
            }
        }

        return string.Empty;
    }
}

