using Xunit;

namespace PortAtlas.Tests;

/// <summary>
/// Serializes the test classes that run Program.Main: it sets Console.OutputEncoding, which replaces Console.Out
/// for the whole process while another test of the class may be writing to it.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ConsoleOutputCollection
{
    public const string Name = "Console output";
}
