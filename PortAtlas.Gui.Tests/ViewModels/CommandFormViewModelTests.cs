using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class CommandFormViewModelTests
{
    private static CommandDescriptor FindCommand(string key) =>
        CommandCatalog.Build().Find(key) ?? throw new InvalidOperationException($"Command '{key}' not found.");

    [Fact]
    public void Preview_WhenFlagIsToggled_ContainsTheOptionName()
    {
        // Arrange
        var vm = new CommandFormViewModel(FindCommand("resolve"), new NullFilePicker());
        var flag = vm.Options.OfType<FlagOptionViewModel>().Single(o => o.Name == "--cache-only");

        // Act
        flag.IsChecked = true;
        var previewWhenChecked = vm.Preview;
        flag.IsChecked = false;

        // Assert
        previewWhenChecked.Should().Be("resolve --cache-only");
        vm.Preview.Should().Be("resolve");
    }

    [Fact]
    public void Preview_WhenTextListGetsTwoValues_RepeatsTheOption()
    {
        // Arrange
        var vm = new CommandFormViewModel(FindCommand("map"), new NullFilePicker());
        var list = vm.Options.OfType<TextListOptionViewModel>().Single(o => o.Name == "--input");

        // Act
        list.AddItemCommand.Execute("nas.csv");
        list.AddItemCommand.Execute("proxmox.csv");

        // Assert
        vm.Preview.Should().Be("map --input nas.csv --input proxmox.csv");
    }

    [Fact]
    public void Groups_WhenCommandHasFilterAndOutputOptions_FollowsTheEnumerationOrder()
    {
        // Act
        var vm = new CommandFormViewModel(FindCommand("export rules"), new NullFilePicker());

        // Assert
        vm.Groups.Select(g => g.Group).Should().Equal(
            OptionGroup.Inputs,
            OptionGroup.Filters,
            OptionGroup.Outputs,
            OptionGroup.Advanced);
        vm.Groups.Should().OnlyContain(g => g.Options.Count > 0);
    }

    [Fact]
    public void LoadState_WhenProfileHoldsValues_FillsTheOptionViewModels()
    {
        // Arrange
        var vm = new CommandFormViewModel(FindCommand("map"), new NullFilePicker());
        var state = new CommandFormState();
        state.SetList("--input", ["nas.csv"]);
        state.SetChoiceOrValue("--layout", "organic");
        state.SetFlag("--listeners-only", true);

        // Act
        vm.LoadState(state);

        // Assert
        vm.Options.OfType<TextListOptionViewModel>().Single(o => o.Name == "--input").Values.Should().Equal("nas.csv");
        vm.Options.OfType<ChoiceOptionViewModel>().Single(o => o.Name == "--layout").SelectedValue.Should().Be("organic");
        vm.Options.OfType<FlagOptionViewModel>().Single(o => o.Name == "--listeners-only").IsChecked.Should().BeTrue();
        vm.Preview.Should().Be("map --input nas.csv --listeners-only --layout organic");
    }

    private sealed class NullFilePicker : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}

file static class StateExtensions
{
    public static void SetChoiceOrValue(this CommandFormState state, string name, string value) =>
        state.SetValue(name, value);
}
