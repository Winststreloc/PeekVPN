using Avalonia;

namespace PeekVPN.App.Maps;

/// <summary>
/// Translates WGS84 coordinates into the authored coordinate space of Assets/world.svg.
/// The SVG uses MapSVG's geoViewBox ordering: west, north, east, south.
/// </summary>
public static class WorldMapProjection
{
    public const double MapWidth = 1009.6727;
    public const double MapHeight = 665.96301;
    public const double WestLongitude = -169.110266;
    public const double EastLongitude = 190.486279;
    public const double NorthLatitude = 83.600842;
    public const double SouthLatitude = -58.508473;

    public static Point Project(double latitude, double longitude)
    {
        var x = (longitude - WestLongitude) / (EastLongitude - WestLongitude) * MapWidth;
        var y = (NorthLatitude - latitude) / (NorthLatitude - SouthLatitude) * MapHeight;
        return new Point(x, y);
    }
}
