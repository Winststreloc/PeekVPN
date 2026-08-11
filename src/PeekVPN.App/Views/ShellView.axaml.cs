using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace PeekVPN.App.Views;

public partial class ShellView : UserControl
{
    private PixelPoint? _dragStartPointerScreenPosition;
    private PixelPoint? _dragStartWindowPosition;

    public ShellView()
    {
        InitializeComponent();
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border || TopLevel.GetTopLevel(this) is not Window window) return;
        if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed) return;

        _dragStartPointerScreenPosition = border.PointToScreen(e.GetPosition(border));
        _dragStartWindowPosition = window.Position;
        e.Pointer.Capture(border);
    }

    private void OnTitleBarPointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Border visual || TopLevel.GetTopLevel(this) is not Window window) return;
        if (_dragStartPointerScreenPosition is not { } startPointer || _dragStartWindowPosition is not { } startWindow) return;

        var currentPointer = visual.PointToScreen(e.GetPosition(visual));
        window.Position = new PixelPoint(
            startWindow.X + (currentPointer.X - startPointer.X),
            startWindow.Y + (currentPointer.Y - startPointer.Y));
    }

    private void OnTitleBarPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragStartPointerScreenPosition = null;
        _dragStartWindowPosition = null;
        e.Pointer.Capture(null);
    }

    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
            window.WindowState = WindowState.Minimized;
    }

    private void OnMaximizeClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
            window.WindowState = window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is Window window)
            window.Close();
    }
}
