using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Styling;
using PeekVPN.Core.State;

namespace PeekVPN.App.Converters;

public sealed class ConnectionStateToBrushConverter : IValueConverter
{
    public static readonly ConnectionStateToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var statusAccent = parameter is string p
                           && p.Equals("status", StringComparison.OrdinalIgnoreCase);

        // Panel status accent: disconnected is red. Latency dots keep green when idle.
        var key = value switch
        {
            VpnConnectionState.Connected => "Brush.Status.Success",
            VpnConnectionState.Disconnected => statusAccent
                ? "Brush.Status.Danger"
                : "Brush.Status.Success",
            VpnConnectionState.Paused => "Brush.Status.Warning",
            VpnConnectionState.Connecting or VpnConnectionState.Disconnecting => "Brush.Status.Neutral",
            _ => "Brush.Status.Neutral"
        };

        return ResolveBrush(key);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static IBrush ResolveBrush(string key)
    {
        if (Application.Current?.TryGetResource(key, ThemeVariant.Default, out var resource) == true
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.Gray;
    }
}
