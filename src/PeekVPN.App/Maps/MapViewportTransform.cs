using Avalonia;

namespace PeekVPN.App.Maps;

/// <summary>Pure viewport math shared by the interactive map and its unit tests.</summary>
public static class MapViewportTransform
{
    // A 1.67x scale leaves roughly 60% of the world-map height in view.
    public const double MinimumZoom = 1.67;
    public const double InitialZoom = MinimumZoom;

    // At a common 2:1 map viewport, this reveals about one third of Europe horizontally.
    public const double MaximumZoom = 20;

    // Focus raises the overview just enough to make the selected server's area legible.
    public const double FocusZoom = 2.6;

    public static readonly Point EuropeFocusPoint = WorldMapProjection.Project(latitude: 54, longitude: 15);

    public static double GetBaseScale(Size viewport) =>
        viewport.Width <= 0 || viewport.Height <= 0
            ? 1
            : Math.Min(
                viewport.Width / WorldMapProjection.MapWidth,
                viewport.Height / WorldMapProjection.MapHeight);

    public static Vector GetBaseOffset(Size viewport, double baseScale) => new(
        (viewport.Width - WorldMapProjection.MapWidth * baseScale) / 2,
        (viewport.Height - WorldMapProjection.MapHeight * baseScale) / 2);

    public static Point MapToScreen(Point mapPoint, Vector baseOffset, Vector pan, double scale) => new(
        baseOffset.X + pan.X + mapPoint.X * scale,
        baseOffset.Y + pan.Y + mapPoint.Y * scale);

    public static Point ScreenToMap(Point screenPoint, Vector baseOffset, Vector pan, double scale)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scale, 0);
        return new Point(
            (screenPoint.X - baseOffset.X - pan.X) / scale,
            (screenPoint.Y - baseOffset.Y - pan.Y) / scale);
    }

    public static bool IsPointWithinScreenRadius(Point point, Point center, double radius)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(radius);
        var delta = point - center;
        return delta.X * delta.X + delta.Y * delta.Y <= radius * radius;
    }

    public static Vector KeepMapPointAtScreenPoint(
        Point screenPoint,
        Point mapPoint,
        Vector baseOffset,
        double scale) => new(
        screenPoint.X - baseOffset.X - mapPoint.X * scale,
        screenPoint.Y - baseOffset.Y - mapPoint.Y * scale);

    public static Vector GetInitialPan(Size viewport)
    {
        var baseScale = GetBaseScale(viewport);
        var baseOffset = GetBaseOffset(viewport, baseScale);
        return KeepMapPointAtScreenPoint(
            new Point(viewport.Width / 2, viewport.Height / 2),
            EuropeFocusPoint,
            baseOffset,
            baseScale * InitialZoom);
    }

    /// <summary>
    /// Calculates the bounded viewport state that brings a map point to the viewport center.
    /// Focus never zooms out from a user's current zoom level.
    /// </summary>
    public static MapFocusTarget GetFocusTarget(Size viewport, Point mapPoint, double currentZoom)
    {
        var zoom = Math.Clamp(Math.Max(currentZoom, FocusZoom), MinimumZoom, MaximumZoom);
        var baseScale = GetBaseScale(viewport);
        var baseOffset = GetBaseOffset(viewport, baseScale);
        var scale = baseScale * zoom;
        var pan = KeepMapPointAtScreenPoint(
            new Point(viewport.Width / 2, viewport.Height / 2),
            mapPoint,
            baseOffset,
            scale);

        return new MapFocusTarget(zoom, ClampPan(viewport, scale, baseOffset, pan));
    }

    public static double EaseOutCubic(double progress)
    {
        var clampedProgress = Math.Clamp(progress, 0, 1);
        return 1 - Math.Pow(1 - clampedProgress, 3);
    }

    public static Vector ClampPan(Size viewport, double scale, Vector baseOffset, Vector pan)
    {
        var x = ClampTranslation(
            baseOffset.X + pan.X,
            viewport.Width,
            WorldMapProjection.MapWidth * scale);
        var y = ClampTranslation(
            baseOffset.Y + pan.Y,
            viewport.Height,
            WorldMapProjection.MapHeight * scale);
        return new Vector(x - baseOffset.X, y - baseOffset.Y);
    }

    private static double ClampTranslation(double translation, double viewport, double map) =>
        map <= viewport
            ? (viewport - map) / 2
            : Math.Clamp(translation, viewport - map, 0);
}

/// <summary>Target zoom and pan for a bounded map-focus transition.</summary>
public readonly record struct MapFocusTarget(double Zoom, Vector Pan);
