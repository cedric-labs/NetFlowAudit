using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Views;

/// <summary>
/// Maps an option section to the icon resource shown in its expander header.
/// </summary>
public sealed class OptionGroupIconConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not OptionGroup group || Application.Current is null)
        {
            return null;
        }

        var key = group switch
        {
            OptionGroup.Inputs => "Icon.Load",
            OptionGroup.Filters => "Icon.Filter",
            OptionGroup.MapRendering => "Icon.Map",
            OptionGroup.Outputs => "Icon.Output",
            OptionGroup.Resolution => "Icon.Resolve",
            _ => "Icon.Advanced"
        };

        return Application.Current.TryFindResource(key, out var icon) ? icon : null;
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
