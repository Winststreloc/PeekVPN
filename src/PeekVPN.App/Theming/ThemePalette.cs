using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using SkiaSharp;

namespace PeekVPN.App.Theming;

/// <summary>
/// Resolves AHUG color tokens for Skia drawing. Must be called on the UI thread —
/// the compositor cannot read <see cref="StyledElement.ActualThemeVariant"/>.
/// </summary>
internal static class ThemePalette
{
    public static SKColor GetSkColor(ThemeVariant? theme, string key, string fallbackHex)
    {
        if (Application.Current?.TryGetResource(key, theme, out var resource) == true)
        {
            return resource switch
            {
                Color color => ToSk(color),
                ISolidColorBrush brush => ToSk(brush.Color),
                _ => SKColor.Parse(fallbackHex)
            };
        }

        return SKColor.Parse(fallbackHex);
    }

    public static MapThemeColors CaptureMapColors(StyledElement owner) => new(
        Land: GetSkColor(owner.ActualThemeVariant, "Color.Map.Land", "#E0D4C4"),
        Connected: GetSkColor(owner.ActualThemeVariant, "Color.Map.MarkerConnected", "#3FA66A"),
        ConnectedRing: GetSkColor(owner.ActualThemeVariant, "Color.Map.MarkerConnectedRing", "#E7F6EC"),
        Paused: GetSkColor(owner.ActualThemeVariant, "Color.Map.MarkerPaused", "#D4A017"),
        PausedFill: GetSkColor(owner.ActualThemeVariant, "Color.Map.MarkerPausedFill", "#F5E6B8"),
        Connecting: GetSkColor(owner.ActualThemeVariant, "Color.Map.Marker", "#C9A227"),
        Idle: GetSkColor(owner.ActualThemeVariant, "Color.Map.MarkerIdle", "#7A6A58"),
        IdleFill: GetSkColor(owner.ActualThemeVariant, "Color.Map.MarkerIdleFill", "#E8DFD3"));

    private static SKColor ToSk(Color color) => new(color.R, color.G, color.B, color.A);
}

internal readonly record struct MapThemeColors(
    SKColor Land,
    SKColor Connected,
    SKColor ConnectedRing,
    SKColor Paused,
    SKColor PausedFill,
    SKColor Connecting,
    SKColor Idle,
    SKColor IdleFill);
