using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PeekVPN.App.Views;

public sealed partial class ProfilePageView : UserControl
{
    public ProfilePageView() => AvaloniaXamlLoader.Load(this);
}
