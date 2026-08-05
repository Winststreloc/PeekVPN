using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PeekVPN.App.Localization;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Owns shell-level window actions and hosts the workspace composition root.
/// </summary>
public sealed partial class ShellViewModel(
    WorkspaceViewModel workspace,
    StatisticsPageViewModel statistics,
    ProfilePageViewModel profile,
    SettingsPageViewModel settings) : ViewModelBase
{
    public WorkspaceViewModel Workspace { get; } = workspace;
    public StatisticsPageViewModel Statistics { get; } = statistics;
    public ProfilePageViewModel Profile { get; } = profile;
    public SettingsPageViewModel Settings { get; } = settings;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentPage))]
    [NotifyPropertyChangedFor(nameof(IsMapPage))]
    [NotifyPropertyChangedFor(nameof(IsMapActive))]
    [NotifyPropertyChangedFor(nameof(IsStatisticsActive))]
    [NotifyPropertyChangedFor(nameof(IsProfileActive))]
    [NotifyPropertyChangedFor(nameof(IsSettingsActive))]
    private ShellPage _selectedPage = ShellPage.Map;

    public string AppTitle => Strings.AppTitle;
    public string NavMap => Strings.NavMap;
    public string NavStatistics => Strings.NavStatistics;
    public string NavProfile => Strings.NavProfile;
    public string NavSettings => Strings.NavSettings;
    public ViewModelBase CurrentPage => SelectedPage switch
    {
        ShellPage.Map => Workspace,
        ShellPage.Statistics => Statistics,
        ShellPage.Profile => Profile,
        ShellPage.Settings => Settings,
        _ => Workspace
    };

    public bool IsMapPage => SelectedPage is ShellPage.Map;
    public bool IsMapActive => SelectedPage is ShellPage.Map;
    public bool IsStatisticsActive => SelectedPage is ShellPage.Statistics;
    public bool IsProfileActive => SelectedPage is ShellPage.Profile;
    public bool IsSettingsActive => SelectedPage is ShellPage.Settings;

    [RelayCommand]
    private void NavigateTo(ShellPage page) => SelectedPage = page;

    [RelayCommand]
    private static void ExitApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
