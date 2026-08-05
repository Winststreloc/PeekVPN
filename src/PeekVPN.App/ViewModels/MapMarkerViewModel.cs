using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using PeekVPN.App.Maps;
using PeekVPN.App.Services;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>Map-ready server metadata and its current VPN session state.</summary>
public sealed partial class MapMarkerViewModel : ViewModelBase
{
    public MapMarkerViewModel(ServerDisplayMetadata server)
    {
        Server = server;
        Position = WorldMapProjection.Project(server.Latitude, server.Longitude);
    }

    public ServerDisplayMetadata Server { get; }

    public string Id => Server.Id;

    public string DisplayName => Server.DisplayName;

    public Point Position { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(IsConnecting))]
    public partial VpnConnectionState ConnectionState { get; set; }

    public bool IsActive => ConnectionState is not VpnConnectionState.Disconnected;

    public bool IsConnecting =>
        ConnectionState is VpnConnectionState.Connecting or VpnConnectionState.Disconnecting;
}
