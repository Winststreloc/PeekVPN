using PeekVPN.App.Maps;

namespace PeekVPN.Core.Tests;

public sealed class WorldMapProjectionTests
{
    [Fact]
    public void Project_geo_view_box_edges_matches_svg_canvas_edges()
    {
        var northWest = WorldMapProjection.Project(
            WorldMapProjection.NorthLatitude,
            WorldMapProjection.WestLongitude);
        var southEast = WorldMapProjection.Project(
            WorldMapProjection.SouthLatitude,
            WorldMapProjection.EastLongitude);

        Assert.Equal(0, northWest.X, 8);
        Assert.Equal(0, northWest.Y, 8);
        Assert.Equal(WorldMapProjection.MapWidth, southEast.X, 8);
        Assert.Equal(WorldMapProjection.MapHeight, southEast.Y, 8);
    }

    [Fact]
    public void Project_increases_x_eastward_and_y_southward()
    {
        var london = WorldMapProjection.Project(51.5072, -0.1276);
        var tokyo = WorldMapProjection.Project(35.6762, 139.6503);

        Assert.True(tokyo.X > london.X);
        Assert.True(tokyo.Y > london.Y);
    }

    [Fact]
    public void KeepMapPointAtScreenPoint_preserves_cursor_position_after_zoom()
    {
        var viewport = new Avalonia.Size(800, 400);
        var baseScale = MapViewportTransform.GetBaseScale(viewport);
        var baseOffset = MapViewportTransform.GetBaseOffset(viewport, baseScale);
        var cursor = new Avalonia.Point(300, 180);
        var mapPoint = new Avalonia.Point(450, 300);
        const double zoomedScale = 1.2;

        var pan = MapViewportTransform.KeepMapPointAtScreenPoint(cursor, mapPoint, baseOffset, zoomedScale);

        Assert.Equal(cursor.X, baseOffset.X + pan.X + mapPoint.X * zoomedScale, 8);
        Assert.Equal(cursor.Y, baseOffset.Y + pan.Y + mapPoint.Y * zoomedScale, 8);
    }

    [Fact]
    public void Cursor_anchor_round_trips_through_shared_pan_and_zoom_transform()
    {
        var viewport = new Avalonia.Size(900, 500);
        var baseScale = MapViewportTransform.GetBaseScale(viewport);
        var baseOffset = MapViewportTransform.GetBaseOffset(viewport, baseScale);
        var originalPan = new Avalonia.Vector(-84, -32);
        var cursor = new Avalonia.Point(702, 146);

        var mapPoint = MapViewportTransform.ScreenToMap(cursor, baseOffset, originalPan, baseScale);
        var zoomedScale = baseScale * 1.75;
        var zoomedPan = MapViewportTransform.KeepMapPointAtScreenPoint(cursor, mapPoint, baseOffset, zoomedScale);
        var anchoredPoint = MapViewportTransform.MapToScreen(mapPoint, baseOffset, zoomedPan, zoomedScale);

        Assert.Equal(cursor.X, anchoredPoint.X, 8);
        Assert.Equal(cursor.Y, anchoredPoint.Y, 8);
    }

    [Fact]
    public void Initial_viewport_centers_on_Europe_and_crops_the_world_to_about_sixty_percent()
    {
        var viewport = new Avalonia.Size(600, 300);
        var baseScale = MapViewportTransform.GetBaseScale(viewport);
        var baseOffset = MapViewportTransform.GetBaseOffset(viewport, baseScale);
        var initialPan = MapViewportTransform.GetInitialPan(viewport);
        var europeScreenPosition = MapViewportTransform.MapToScreen(
            MapViewportTransform.EuropeFocusPoint,
            baseOffset,
            initialPan,
            baseScale * MapViewportTransform.InitialZoom);
        var visibleWorldHeightFraction =
            viewport.Height / (WorldMapProjection.MapHeight * baseScale * MapViewportTransform.InitialZoom);

        Assert.Equal(viewport.Width / 2, europeScreenPosition.X, 8);
        Assert.Equal(viewport.Height / 2, europeScreenPosition.Y, 8);
        Assert.InRange(visibleWorldHeightFraction, 0.58, 0.62);
    }

    [Fact]
    public void Maximum_zoom_shows_about_one_third_of_Europe_horizontally()
    {
        var viewport = new Avalonia.Size(600, 300);
        var baseScale = MapViewportTransform.GetBaseScale(viewport);
        var westernEurope = WorldMapProjection.Project(latitude: 54, longitude: -10);
        var easternEurope = WorldMapProjection.Project(latitude: 54, longitude: 60);
        var europeWidth = easternEurope.X - westernEurope.X;
        var visibleMapWidth = viewport.Width / (baseScale * MapViewportTransform.MaximumZoom);

        Assert.Equal(1.67, MapViewportTransform.MinimumZoom, 2);
        Assert.Equal(MapViewportTransform.MinimumZoom, MapViewportTransform.InitialZoom, 8);
        Assert.InRange(visibleMapWidth / europeWidth, 0.30, 0.38);
    }

    [Fact]
    public void Marker_hit_geometry_uses_transformed_screen_position_and_radius()
    {
        var markerPosition = new Avalonia.Point(420, 260);
        var screenCenter = MapViewportTransform.MapToScreen(
            markerPosition,
            new Avalonia.Vector(30, 20),
            new Avalonia.Vector(-120, 45),
            scale: 1.5);

        Assert.True(MapViewportTransform.IsPointWithinScreenRadius(
            new Avalonia.Point(screenCenter.X + 11.9, screenCenter.Y),
            screenCenter,
            radius: 12));
        Assert.False(MapViewportTransform.IsPointWithinScreenRadius(
            new Avalonia.Point(screenCenter.X + 12.1, screenCenter.Y),
            screenCenter,
            radius: 12));
    }

    [Fact]
    public void ClampPan_prevents_revealing_space_beyond_a_zoomed_map_edge()
    {
        var viewport = new Avalonia.Size(800, 400);
        const double scale = 1;
        var baseOffset = MapViewportTransform.GetBaseOffset(viewport, scale);

        var clamped = MapViewportTransform.ClampPan(viewport, scale, baseOffset, new Avalonia.Vector(2000, -2000));

        Assert.Equal(0, baseOffset.X + clamped.X, 8);
        Assert.Equal(viewport.Height - WorldMapProjection.MapHeight, baseOffset.Y + clamped.Y, 8);
    }

    [Fact]
    public void Focus_target_centers_marker_and_zooms_in_without_exceeding_bounds()
    {
        var viewport = new Avalonia.Size(800, 400);
        var marker = WorldMapProjection.Project(latitude: 51.5072, longitude: -0.1276);
        var target = MapViewportTransform.GetFocusTarget(
            viewport,
            marker,
            MapViewportTransform.InitialZoom);
        var baseScale = MapViewportTransform.GetBaseScale(viewport);
        var baseOffset = MapViewportTransform.GetBaseOffset(viewport, baseScale);
        var screenPosition = MapViewportTransform.MapToScreen(
            marker,
            baseOffset,
            target.Pan,
            baseScale * target.Zoom);

        Assert.Equal(MapViewportTransform.FocusZoom, target.Zoom, 8);
        Assert.Equal(viewport.Width / 2, screenPosition.X, 8);
        Assert.Equal(viewport.Height / 2, screenPosition.Y, 8);
        Assert.Equal(
            target.Pan,
            MapViewportTransform.ClampPan(viewport, baseScale * target.Zoom, baseOffset, target.Pan));
    }

    [Fact]
    public void Focus_target_preserves_a_higher_user_zoom_clamps_edge_markers_and_eases()
    {
        var viewport = new Avalonia.Size(800, 400);
        var target = MapViewportTransform.GetFocusTarget(
            viewport,
            WorldMapProjection.Project(latitude: 35.6762, longitude: 139.6503),
            currentZoom: 5);
        var baseScale = MapViewportTransform.GetBaseScale(viewport);
        var baseOffset = MapViewportTransform.GetBaseOffset(viewport, baseScale);

        Assert.Equal(5, target.Zoom, 8);
        Assert.Equal(
            target.Pan,
            MapViewportTransform.ClampPan(viewport, baseScale * target.Zoom, baseOffset, target.Pan));
        Assert.Equal(0, MapViewportTransform.EaseOutCubic(0), 8);
        Assert.Equal(1, MapViewportTransform.EaseOutCubic(1), 8);
        Assert.True(MapViewportTransform.EaseOutCubic(0.5) > 0.5);
    }
}
