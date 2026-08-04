using System;
using Avalonia;
using Avalonia.Media;

namespace PeekVPN.App;

sealed class Program
{
    private const string InterFontFamily = "avares://PeekVPN/Assets/Inter.ttf#Inter";

    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .With(new FontManagerOptions
            {
                DefaultFamilyName = InterFontFamily
            })
            .LogToTrace();
}
