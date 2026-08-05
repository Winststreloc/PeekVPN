using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PeekVPN.App.Views;

public sealed partial class SettingsPageView : UserControl
{
    public SettingsPageView() => AvaloniaXamlLoader.Load(this);
}
