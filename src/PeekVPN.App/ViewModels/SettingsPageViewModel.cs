using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia;
using Avalonia.Styling;
using PeekVPN.App.Localization;
using System.Collections.ObjectModel;

namespace PeekVPN.App.ViewModels;

public enum SettingsSection
{
    General,
    Appearance,
    VpnConnection,
    KillSwitch,
    SplitTunneling,
}

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppearanceSelected))]
    [NotifyPropertyChangedFor(nameof(IsKillSwitchSelected))]
    [NotifyPropertyChangedFor(nameof(IsVpnConnectionSelected))]
    [NotifyPropertyChangedFor(nameof(IsSplitTunnelingSelected))]
    [NotifyPropertyChangedFor(nameof(IsGeneralSelected))]
    public partial SettingsSection SelectedSection { get; set; } = SettingsSection.General;

    [ObservableProperty]
    public partial bool AutoConnect { get; set; } = true;

    [ObservableProperty]
    public partial bool AutoConnectUseFastestServer { get; set; } = true;

    [ObservableProperty]
    public partial string AutoConnectServer { get; set; } = Strings.SettingsServerPlaceholder;

    [ObservableProperty]
    public partial bool SplitTunneling { get; set; }

    [ObservableProperty]
    private string _newSplitTunnelingDomain = string.Empty;

    [ObservableProperty]
    private string _newSplitTunnelingApplication = string.Empty;

    [ObservableProperty]
    private bool _killSwitch = true;

    [ObservableProperty]
    private string _selectedKillSwitchMode = Strings.SettingsKillSwitchSoftMode;

    [ObservableProperty]
    private string _vpnProtocol = Strings.SettingsProtocolRecommended;

    [ObservableProperty]
    private bool _customDns;

    [ObservableProperty]
    private bool _autoStartup;

    [ObservableProperty]
    private bool _stayHidden;

    [ObservableProperty]
    private bool _autoUpdates = true;

    [ObservableProperty]
    private bool _allowBackgroundProcesses = true;

    [ObservableProperty]
    private string _appearance = Strings.SettingsAppearanceLight;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private bool _sendAppLogs;

    public IReadOnlyList<string> AutoConnectServers { get; } =
    [
        Strings.SettingsServerPlaceholder,
        Strings.SettingsServerAmsterdam,
        Strings.SettingsServerNewYork,
        Strings.SettingsServerSingapore
    ];

    public IReadOnlyList<string> KillSwitchModes { get; } =
    [
        Strings.SettingsKillSwitchSoftMode,
        Strings.SettingsKillSwitchHardMode
    ];

    public IReadOnlyList<string> VpnProtocols { get; } =
    [
        Strings.SettingsProtocolRecommended,
        Strings.SettingsProtocolOpenVpn,
        Strings.SettingsProtocolIkev2
    ];

    public IReadOnlyList<string> AppearanceOptions { get; } =
    [
        Strings.SettingsAppearanceSystem,
        Strings.SettingsAppearanceLight,
        Strings.SettingsAppearanceDark
    ];

    public bool IsSplitTunnelingSelected => SelectedSection is SettingsSection.SplitTunneling;
    public bool IsKillSwitchSelected => SelectedSection is SettingsSection.KillSwitch;
    public bool IsVpnConnectionSelected => SelectedSection is SettingsSection.VpnConnection;
    public bool IsGeneralSelected => SelectedSection is SettingsSection.General;
    public bool IsAppearanceSelected => SelectedSection is SettingsSection.Appearance;
    public bool IsAutoConnectTargetEnabled => true;
    public bool IsAutoConnectServerSelectionEnabled => !AutoConnectUseFastestServer;
    public bool IsSplitTunnelingOptionsEnabled => true;
    public bool IsKillSwitchModeEnabled => true;
    public bool IsSplitTunnelingDomainsEmpty => SplitTunnelingDomains.Count is 0;
    public bool IsSplitTunnelingApplicationsEmpty => SplitTunnelingApplications.Count is 0;
    public ObservableCollection<string> SplitTunnelingDomains { get; } = [];
    public ObservableCollection<string> SplitTunnelingApplications { get; } = [];

    public SettingsPageViewModel()
    {
        SplitTunnelingDomains.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsSplitTunnelingDomainsEmpty));
        SplitTunnelingApplications.CollectionChanged += (_, _) => OnPropertyChanged(nameof(IsSplitTunnelingApplicationsEmpty));
    }

    [RelayCommand]
    private void SelectSection(SettingsSection section) => SelectedSection = section;

    partial void OnAutoConnectUseFastestServerChanged(bool value)
        => OnPropertyChanged(nameof(IsAutoConnectServerSelectionEnabled));

    partial void OnAppearanceChanged(string value)
    {
        if (Application.Current is not { } application)
        {
            return;
        }

        application.RequestedThemeVariant = value switch
        {
            var appearance when appearance == Strings.SettingsAppearanceDark => ThemeVariant.Dark,
            var appearance when appearance == Strings.SettingsAppearanceSystem => ThemeVariant.Default,
            _ => ThemeVariant.Light
        };
    }

    [RelayCommand]
    private void AddSplitTunnelingDomain()
    {
        if (string.IsNullOrWhiteSpace(NewSplitTunnelingDomain))
        {
            return;
        }

        SplitTunnelingDomains.Add(NewSplitTunnelingDomain.Trim());
        NewSplitTunnelingDomain = string.Empty;
    }

    [RelayCommand]
    private void AddSplitTunnelingApplication()
    {
        if (string.IsNullOrWhiteSpace(NewSplitTunnelingApplication))
        {
            return;
        }

        SplitTunnelingApplications.Add(NewSplitTunnelingApplication.Trim());
        NewSplitTunnelingApplication = string.Empty;
    }

    [RelayCommand]
    private void ResetSettings()
    {
        AutoConnect = true;
        AutoConnectUseFastestServer = true;
        AutoConnectServer = Strings.SettingsServerPlaceholder;
        SplitTunneling = false;
        NewSplitTunnelingDomain = string.Empty;
        NewSplitTunnelingApplication = string.Empty;
        SplitTunnelingDomains.Clear();
        SplitTunnelingApplications.Clear();
        KillSwitch = true;
        SelectedKillSwitchMode = Strings.SettingsKillSwitchSoftMode;
        VpnProtocol = Strings.SettingsProtocolRecommended;
        CustomDns = false;
        AutoStartup = false;
        StayHidden = false;
        AutoUpdates = true;
        AllowBackgroundProcesses = true;
        Appearance = Strings.SettingsAppearanceLight;
        ShowNotifications = true;
        SendAppLogs = false;
    }
}
