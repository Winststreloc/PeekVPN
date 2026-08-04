using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.Input;
using PeekVPN.App.Localization;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Owns shell-level window actions and hosts the workspace composition root.
/// </summary>
public sealed partial class ShellViewModel(WorkspaceViewModel workspace) : ViewModelBase
{
    public WorkspaceViewModel Workspace { get; } = workspace;

    public string AppTitle => Strings.AppTitle;
    public string SearchServersWatermark => Strings.SearchServersWatermark;
    public string NavMap => Strings.NavMap;
    public string NavStatistic => Strings.NavStatistic;
    public string NavProfile => Strings.NavProfile;
    public string NavSettings => Strings.NavSettings;

    [RelayCommand]
    private static void ExitApp()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
