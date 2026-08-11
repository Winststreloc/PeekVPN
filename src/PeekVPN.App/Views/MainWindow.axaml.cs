using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace PeekVPN.App.Views;

public partial class MainWindow : Window
{
    private WindowEdge? _resizeEdge;
    private PixelPoint _resizeStartPointerScreenPosition;
    private PixelPoint _resizeStartWindowPosition;
    private Size _resizeStartWindowSize;

    public MainWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ApplyRoundedCorners();
    }

    // ponytail: manual resize, same reason as ShellView's manual drag - BeginResizeDrag uses the
    // same broken OS non-client trick as BeginMoveDrag, which doesn't work in this configuration.
    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: WindowEdge edge } control) return;
        if (!e.GetCurrentPoint(control).Properties.IsLeftButtonPressed) return;

        _resizeEdge = edge;
        _resizeStartPointerScreenPosition = control.PointToScreen(e.GetPosition(control));
        _resizeStartWindowPosition = Position;
        _resizeStartWindowSize = new Size(Width, Height);
        e.Pointer.Capture(control);
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control || _resizeEdge is not { } edge) return;

        var currentPointer = control.PointToScreen(e.GetPosition(control));
        var dx = currentPointer.X - _resizeStartPointerScreenPosition.X;
        var dy = currentPointer.Y - _resizeStartPointerScreenPosition.Y;

        var startX = _resizeStartWindowPosition.X;
        var startY = _resizeStartWindowPosition.Y;
        var startWidth = _resizeStartWindowSize.Width;
        var startHeight = _resizeStartWindowSize.Height;

        double newWidth = startWidth, newHeight = startHeight, newX = startX, newY = startY;

        if (edge is WindowEdge.West or WindowEdge.NorthWest or WindowEdge.SouthWest)
        {
            newWidth = Math.Max(MinWidth, startWidth - dx);
            newX = startX + startWidth - newWidth;
        }
        else if (edge is WindowEdge.East or WindowEdge.NorthEast or WindowEdge.SouthEast)
        {
            newWidth = Math.Max(MinWidth, startWidth + dx);
        }

        if (edge is WindowEdge.North or WindowEdge.NorthWest or WindowEdge.NorthEast)
        {
            newHeight = Math.Max(MinHeight, startHeight - dy);
            newY = startY + startHeight - newHeight;
        }
        else if (edge is WindowEdge.South or WindowEdge.SouthWest or WindowEdge.SouthEast)
        {
            newHeight = Math.Max(MinHeight, startHeight + dy);
        }

        Width = newWidth;
        Height = newHeight;
        Position = new PixelPoint((int)newX, (int)newY);
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _resizeEdge = null;
        e.Pointer.Capture(null);
    }

    // ponytail: Windows-only DWM rounding; WindowDecorations="None" opts out of the OS's default
    // rounded-corner chrome on Windows 11, so we have to ask for it back explicitly.
    private void ApplyRoundedCorners()
    {
        if (!OperatingSystem.IsWindows()) return;
        if (TryGetPlatformHandle() is not { } handle) return;

        var preference = DwmWindowCornerPreference.Round;
        DwmSetWindowAttribute(handle.Handle, DwmWindowAttribute.WindowCornerPreference, ref preference, sizeof(DwmWindowCornerPreference));
    }

    private enum DwmWindowAttribute
    {
        WindowCornerPreference = 33,
    }

    private enum DwmWindowCornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3,
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        IntPtr hwnd, DwmWindowAttribute attribute, ref DwmWindowCornerPreference pvAttribute, int cbAttribute);
}
