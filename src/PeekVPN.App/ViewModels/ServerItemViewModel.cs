using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using PeekVPN.App.Helpers;
using PeekVPN.App.Localization;
using PeekVPN.App.Services;
using PeekVPN.Contracts;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

public partial class ServerItemViewModel : ViewModelBase
{
    private static readonly ICommand NoOpCommand = new RelayCommand(static () => { });

    public ServerItemViewModel(VpnServerDto server)
        : this(server, NoOpCommand)
    {
    }

    public ServerItemViewModel(VpnServerDto server, ICommand connectServerCommand)
        : this(
            new ServerDisplayMetadata(
                server.Id,
                server.City,
                server.Country,
                server.CountryCode,
                server.LatencyMs,
                server.DisplayName,
                CountryFlagAssets.GetUri(server.CountryCode)),
            connectServerCommand)
    {
    }

    public ServerItemViewModel(ServerDisplayMetadata server, ICommand connectServerCommand)
    {
        Id = server.Id;
        City = server.City;
        Country = server.Country;
        CityLabel = server.DisplayName;
        CountryCode = server.CountryCode;
        FlagUri = CountryFlagAssets.GetUri(server.CountryCode);
        LatencyMs = server.LatencyMs;
        LatencyText = string.Format(Strings.LatencyFormat, server.LatencyMs);
        ConnectServerCommand = connectServerCommand;
    }

    public string Id { get; }
    public string City { get; }
    public string Country { get; }
    public string CityLabel { get; }
    public string CountryCode { get; }
    public Uri? FlagUri { get; }
    public int LatencyMs { get; }
    public string LatencyText { get; }
    public ICommand ConnectServerCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyPropertyChangedFor(nameof(IsConnectedHighlight))]
    [NotifyPropertyChangedFor(nameof(IsConnectingHighlight))]
    [NotifyPropertyChangedFor(nameof(IsPausedHighlight))]
    public partial VpnConnectionState ConnectionState { get; set; } = VpnConnectionState.Disconnected;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool IsActive => ConnectionState is not VpnConnectionState.Disconnected;

    public bool IsIdle => !IsActive;

    public bool IsConnectedHighlight => ConnectionState is VpnConnectionState.Connected;

    public bool IsConnectingHighlight =>
        ConnectionState is VpnConnectionState.Connecting or VpnConnectionState.Disconnecting;

    public bool IsPausedHighlight => ConnectionState is VpnConnectionState.Paused;
}
