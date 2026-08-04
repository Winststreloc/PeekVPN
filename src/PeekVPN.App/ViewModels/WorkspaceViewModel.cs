using System.ComponentModel;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Composition boundary for feature view-models and workspace layout.
/// </summary>
public sealed class WorkspaceViewModel : ViewModelBase, IDisposable
{
    public WorkspaceViewModel(
        ServerBrowserViewModel serverBrowser,
        ConnectionPanelViewModel connectionPanel,
        StatsSummaryViewModel statsSummary,
        MapViewModel map,
        FeatureCardsViewModel featureCards)
    {
        ServerBrowser = serverBrowser;
        ConnectionPanel = connectionPanel;
        StatsSummary = statsSummary;
        Map = map;
        FeatureCards = featureCards;
        ServerBrowser.PropertyChanged += OnServerBrowserPropertyChanged;

        SyncSelectedServer();
    }

    public ServerBrowserViewModel ServerBrowser { get; }
    public ConnectionPanelViewModel ConnectionPanel { get; }
    public StatsSummaryViewModel StatsSummary { get; }
    public MapViewModel Map { get; }
    public FeatureCardsViewModel FeatureCards { get; }

    public void Dispose()
    {
        ServerBrowser.PropertyChanged -= OnServerBrowserPropertyChanged;
        ServerBrowser.Dispose();
        ConnectionPanel.Dispose();
        StatsSummary.Dispose();
        Map.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnServerBrowserPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ServerBrowserViewModel.SelectedServer))
        {
            SyncSelectedServer();
        }
    }

    private void SyncSelectedServer() =>
        ConnectionPanel.SelectedServerId = ServerBrowser.SelectedServer?.Id;
}
