using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PeekVPN.App.Maps;
using PeekVPN.App.Theming;
using PeekVPN.App.ViewModels;
using PeekVPN.Core.State;
using SkiaSharp;
using Svg.Skia;

namespace PeekVPN.App.Controls;

/// <summary>
/// A self-contained vector world-map viewport with cursor-centred zoom, constrained panning,
/// and screen-space server markers. The SVG picture is drawn directly by Skia for every frame;
/// it is never converted into a fixed-size bitmap.
/// </summary>
public sealed class InteractiveMapControl : Control
{
    private const float MarkerRadius = 8;
    private const double MarkerHitRadius = 12;
    private const double DragThreshold = 4;
    private const double PulsePeriodSeconds = 1.8;

    public static readonly StyledProperty<IEnumerable<MapMarkerViewModel>?> MarkersProperty =
        AvaloniaProperty.Register<InteractiveMapControl, IEnumerable<MapMarkerViewModel>?>(nameof(Markers));

    public static readonly StyledProperty<MapMarkerViewModel?> FocusTargetProperty =
        AvaloniaProperty.Register<InteractiveMapControl, MapMarkerViewModel?>(nameof(FocusTarget));

    public static readonly StyledProperty<ICommand?> MarkerCommandProperty =
        AvaloniaProperty.Register<InteractiveMapControl, ICommand?>(nameof(MarkerCommand));

    private static readonly Lazy<SKPicture?> MapPicture = new(LoadMapPicture);
    // This timer drives both the short focus transition and the connected-marker pulse so
    // the control maintains at most one render loop.
    private readonly DispatcherTimer _animationTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Dictionary<MapMarkerViewModel, PropertyChangedEventHandler> _markerSubscriptions = [];
    private INotifyCollectionChanged? _markersCollection;
    private Point? _dragStart;
    private Point? _pressPosition;
    private MapMarkerViewModel? _pressedMarker;
    private Point? _pendingFocusPoint;
    private Vector _pan;
    private Vector _animationStartPan;
    private Vector _animationTargetPan;
    private double _animationStartZoom;
    private double _animationTargetZoom;
    private DateTime _animationStartedAt;
    private DateTime _pulseStartedAt;
    private double _zoom = MapViewportTransform.InitialZoom;
    private bool _focusAnimationRunning;
    private bool _pulseAnimationRunning;
    private bool _isAttachedToVisualTree;
    private bool _initialViewportApplied;

    public InteractiveMapControl()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        _animationTimer.Tick += OnAnimationTick;
    }

    public IEnumerable<MapMarkerViewModel>? Markers
    {
        get => GetValue(MarkersProperty);
        set => SetValue(MarkersProperty, value);
    }

    public MapMarkerViewModel? FocusTarget
    {
        get => GetValue(FocusTargetProperty);
        set => SetValue(FocusTargetProperty, value);
    }

    public ICommand? MarkerCommand
    {
        get => GetValue(MarkerCommandProperty);
        set => SetValue(MarkerCommandProperty, value);
    }

    static InteractiveMapControl()
    {
        MarkersProperty.Changed.AddClassHandler<InteractiveMapControl>((control, _) => control.RebuildMarkers());
        FocusTargetProperty.Changed.AddClassHandler<InteractiveMapControl>((control, _) =>
        {
            if (control.FocusTarget is { } marker)
            {
                control.AnimateFocus(marker.Position);
            }
        });
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var arranged = base.ArrangeOverride(finalSize);
        if (!_initialViewportApplied && arranged.Width > 0 && arranged.Height > 0)
        {
            _pan = MapViewportTransform.GetInitialPan(arranged);
            _initialViewportApplied = true;
        }
        else
        {
            ClampPan();
        }

        if (_pendingFocusPoint is { } focusPoint)
        {
            AnimateFocus(focusPoint);
        }

        InvalidateVisual();
        return arranged;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        context.Custom(new MapDrawOperation(
            new Rect(Bounds.Size),
            MapPicture.Value,
            EffectiveScale,
            BaseOffset + _pan,
            Markers ?? [],
            _pulseAnimationRunning
                ? (DateTime.UtcNow - _pulseStartedAt).TotalSeconds / PulsePeriodSeconds % 1
                : 0,
            ThemePalette.CaptureMapColors(this)));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _isAttachedToVisualTree = true;
        if (Application.Current is { } application)
        {
            application.ActualThemeVariantChanged += OnActualThemeVariantChanged;
        }

        UpdateAnimationTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (Application.Current is { } application)
        {
            application.ActualThemeVariantChanged -= OnActualThemeVariantChanged;
        }

        _isAttachedToVisualTree = false;
        _animationTimer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnActualThemeVariantChanged(object? sender, EventArgs e) => InvalidateVisual();

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            StopFocusAnimation();
            var position = e.GetPosition(this);
            _pressedMarker = HitTestMarker(position);
            _pressPosition = position;
            _dragStart = _pressedMarker is null ? position : null;
            e.Pointer.Capture(this);
            e.Handled = true;
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var pointer = e.GetPosition(this);
        if (_pressedMarker is not null)
        {
            if (_pressPosition is not { } pressPosition
                || MapViewportTransform.IsPointWithinScreenRadius(pointer, pressPosition, DragThreshold))
            {
                return;
            }

            _pressedMarker = null;
            _dragStart = _pressPosition;
        }

        if (_dragStart is not { } dragStart)
        {
            return;
        }

        _pan += pointer - dragStart;
        _dragStart = pointer;
        ClampPan();
        InvalidateVisual();
        Cursor = new Cursor(StandardCursorType.SizeAll);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_pressPosition is not null)
        {
            var marker = _pressedMarker;
            var releasedOnMarker = marker is not null && HitTestMarker(e.GetPosition(this)) == marker;
            _dragStart = null;
            _pressPosition = null;
            _pressedMarker = null;
            e.Pointer.Capture(null);
            Cursor = new Cursor(StandardCursorType.Hand);
            if (releasedOnMarker)
            {
                // Start locally so repeated clicks focus immediately, even when the binding's
                // FocusTarget value did not change.
                AnimateFocus(marker!.Position);
                if (MarkerCommand?.CanExecute(marker) == true)
                {
                    MarkerCommand.Execute(marker);
                }
            }

            e.Handled = true;
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        StopFocusAnimation();
        var oldScale = EffectiveScale;
        if (oldScale <= 0)
        {
            return;
        }

        var cursor = e.GetPosition(this);
        var mapPoint = MapViewportTransform.ScreenToMap(cursor, BaseOffset, _pan, oldScale);
        var zoomFactor = Math.Pow(1.16, e.Delta.Y);
        _zoom = Math.Clamp(
            _zoom * zoomFactor,
            MapViewportTransform.MinimumZoom,
            MapViewportTransform.MaximumZoom);

        var newScale = EffectiveScale;
        _pan = MapViewportTransform.KeepMapPointAtScreenPoint(cursor, mapPoint, BaseOffset, newScale);
        ClampPan();
        InvalidateVisual();
        e.Handled = true;
    }

    private double BaseScale => MapViewportTransform.GetBaseScale(Bounds.Size);

    private double EffectiveScale => BaseScale * _zoom;

    private Vector BaseOffset => MapViewportTransform.GetBaseOffset(Bounds.Size, BaseScale);

    private void RebuildMarkers()
    {
        if (_markersCollection is not null)
        {
            _markersCollection.CollectionChanged -= OnMarkersCollectionChanged;
        }

        _markersCollection = Markers as INotifyCollectionChanged;
        if (_markersCollection is not null)
        {
            _markersCollection.CollectionChanged += OnMarkersCollectionChanged;
        }

        foreach (var (marker, handler) in _markerSubscriptions)
        {
            marker.PropertyChanged -= handler;
        }

        _markerSubscriptions.Clear();
        foreach (var marker in Markers ?? [])
        {
            PropertyChangedEventHandler handler = (_, args) =>
            {
                if (args.PropertyName is nameof(MapMarkerViewModel.ConnectionState))
                {
                    UpdateAnimationTimer();
                }

                InvalidateVisual();
            };
            marker.PropertyChanged += handler;
            _markerSubscriptions.Add(marker, handler);
        }

        UpdateAnimationTimer();
        InvalidateVisual();
    }

    private void OnMarkersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => RebuildMarkers();

    private void ClampPan()
    {
        var scale = EffectiveScale;
        if (scale <= 0 || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _pan = MapViewportTransform.ClampPan(Bounds.Size, scale, BaseOffset, _pan);
    }

    private MapMarkerViewModel? HitTestMarker(Point point)
    {
        foreach (var marker in Markers ?? [])
        {
            var screenPosition = MapViewportTransform.MapToScreen(marker.Position, BaseOffset, _pan, EffectiveScale);
            if (MapViewportTransform.IsPointWithinScreenRadius(point, screenPosition, MarkerHitRadius))
            {
                return marker;
            }
        }

        return null;
    }

    private void AnimateFocus(Point mapPoint)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            _pendingFocusPoint = mapPoint;
            return;
        }

        _pendingFocusPoint = null;
        _animationStartPan = _pan;
        _animationStartZoom = _zoom;
        var target = MapViewportTransform.GetFocusTarget(Bounds.Size, mapPoint, _zoom);
        _animationTargetPan = target.Pan;
        _animationTargetZoom = target.Zoom;
        _animationStartedAt = DateTime.UtcNow;
        _focusAnimationRunning = true;
        UpdateAnimationTimer();
    }

    private void OnAnimationTick(object? sender, EventArgs e)
    {
        if (_focusAnimationRunning)
        {
            const double durationMilliseconds = 280;
            var progress = Math.Clamp((DateTime.UtcNow - _animationStartedAt).TotalMilliseconds / durationMilliseconds, 0, 1);
            var eased = MapViewportTransform.EaseOutCubic(progress);
            _zoom = _animationStartZoom + (_animationTargetZoom - _animationStartZoom) * eased;
            _pan = _animationStartPan + (_animationTargetPan - _animationStartPan) * eased;
            ClampPan();
            if (progress >= 1)
            {
                _focusAnimationRunning = false;
            }
        }

        InvalidateVisual();
        UpdateAnimationTimer();
    }

    private void StopFocusAnimation()
    {
        _focusAnimationRunning = false;
        UpdateAnimationTimer();
    }

    private void UpdateAnimationTimer()
    {
        var hasConnectedMarker = (Markers ?? []).Any(marker => marker.ConnectionState is VpnConnectionState.Connected);
        if (hasConnectedMarker && !_pulseAnimationRunning)
        {
            _pulseStartedAt = DateTime.UtcNow;
        }

        _pulseAnimationRunning = hasConnectedMarker;
        if (_isAttachedToVisualTree && (_focusAnimationRunning || _pulseAnimationRunning))
        {
            _animationTimer.Start();
        }
        else
        {
            _animationTimer.Stop();
        }
    }

    private static SKPicture? LoadMapPicture()
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://PeekVPN/Assets/world.svg"));
            var svg = new SKSvg();
            svg.Load(stream);
            return svg.Picture;
        }
        catch
        {
            return null;
        }
    }

    private sealed class MapDrawOperation(
        Rect bounds,
        SKPicture? picture,
        double scale,
        Vector translation,
        IEnumerable<MapMarkerViewModel> markers,
        double pulsePhase,
        MapThemeColors theme) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature is null)
            {
                return;
            }

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, 0, (float)Bounds.Width, (float)Bounds.Height));

            using var markerFill = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill };
            using var markerStroke = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2,
            };
            using var pulsePaint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 2.5f),
            };

            if (picture is not null)
            {
                canvas.Translate((float)translation.X, (float)translation.Y);
                canvas.Scale((float)scale);
                using var landPaint = new SKPaint
                {
                    IsAntialias = true,
                    ColorFilter = SKColorFilter.CreateBlendMode(theme.Land, SKBlendMode.SrcIn),
                };
                canvas.DrawPicture(picture, landPaint);
                canvas.Restore();
                canvas.Save();
            }

            foreach (var marker in markers)
            {
                var position = MapViewportTransform.MapToScreen(marker.Position, Vector.Zero, translation, scale);
                if (marker.ConnectionState is VpnConnectionState.Connected)
                {
                    // Keep the aura entirely in screen space, matching the marker's fixed
                    // display size regardless of the map zoom.
                    var progress = (float)((1 - Math.Cos(pulsePhase * Math.PI * 2)) / 2);
                    var radius = MarkerRadius + 2 + progress * 4;
                    pulsePaint.Color = theme.Connected.WithAlpha((byte)(48 * (1 - progress) + 8));
                    canvas.DrawCircle((float)position.X, (float)position.Y, radius, pulsePaint);
                }

                (markerFill.Color, markerStroke.Color) = marker.ConnectionState switch
                {
                    VpnConnectionState.Connected => (theme.Connected, theme.ConnectedRing),
                    VpnConnectionState.Paused => (theme.PausedFill, theme.Paused),
                    VpnConnectionState.Connecting or VpnConnectionState.Disconnecting =>
                        (theme.Connecting, theme.Connected),
                    _ => (theme.IdleFill, theme.Idle),
                };
                canvas.DrawCircle((float)position.X, (float)position.Y, MarkerRadius, markerFill);
                canvas.DrawCircle((float)position.X, (float)position.Y, MarkerRadius, markerStroke);
            }

            canvas.Restore();
        }

        public bool HitTest(Point point) => Bounds.Contains(point);

        public bool Equals(ICustomDrawOperation? other) => false;

        public void Dispose()
        {
        }
    }
}
