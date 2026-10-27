using Xunit;

namespace NetFlowAudit.Tests;

/// <summary>
/// Serializes test classes that touch the process-wide console: Program.Main sets Console.OutputEncoding,
/// which replaces Console.Out and would discard the writer captured by a test running in parallel.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ConsoleOutputCollection
{
    public const string Name = "Console output";
}
