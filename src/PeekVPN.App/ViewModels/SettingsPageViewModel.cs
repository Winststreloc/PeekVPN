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
    VpnConnection,
    SecurityAndPrivacy
}

public sealed partial class SettingsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVpnConnectionSelected))]
    [NotifyPropertyChangedFor(nameof(IsSecurityAndPrivacySelected))]
    [NotifyPropertyChangedFor(nameof(IsGeneralSelected))]
    private SettingsSection _selectedSection = SettingsSection.General;

    [ObservableProperty]
    private bool _autoConnect = true;

    [ObservableProperty]
    private bool _autoConnectUseFastestServer = true;

    [ObservableProperty]
    private string _autoConnectServer = Strings.SettingsServerPlaceholder;

    [ObservableProperty]
    private bool _splitTunneling;

    [ObservableProperty]
    private bool _splitTunnelingForDomains;

    [ObservableProperty]
    private bool _splitTunnelingForApplications;

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

    public string VpnConnectionSectionTitle => Strings.SettingsVpnConnectionSection;
    public string SecurityAndPrivacySectionTitle => Strings.SettingsSecurityAndPrivacySection;
    public string GeneralSectionTitle => Strings.SettingsGeneralSection;
    public string AutoConnectTitle => Strings.SettingsAutoConnectTitle;
    public string AutoConnectDescription => Strings.SettingsAutoConnectDescription;
    public string AutoConnectTargetTitle => Strings.SettingsAutoConnectTargetTitle;
    public string AutoConnectTargetDescription => Strings.SettingsAutoConnectTargetDescription;
    public string FastestServer => Strings.SettingsFastestServer;
    public string AutoConnectServerTitle => Strings.SettingsAutoConnectServerTitle;
    public string AutoConnectServerDescription => Strings.SettingsAutoConnectServerDescription;
    public string SplitTunnelingTitle => Strings.SettingsSplitTunnelingTitle;
    public string SplitTunnelingDescription => Strings.SettingsSplitTunnelingDescription;
    public string SplitTunnelingDomainsTitle => Strings.SettingsSplitTunnelingDomainsTitle;
    public string SplitTunnelingDomainsDescription => Strings.SettingsSplitTunnelingDomainsDescription;
    public string SplitTunnelingApplicationsTitle => Strings.SettingsSplitTunnelingApplicationsTitle;
    public string SplitTunnelingApplicationsDescription => Strings.SettingsSplitTunnelingApplicationsDescription;
    public string SplitTunnelingAddDomainWatermark => Strings.SettingsSplitTunnelingAddDomainWatermark;
    public string SplitTunnelingAddApplicationWatermark => Strings.SettingsSplitTunnelingAddApplicationWatermark;
    public string SplitTunnelingAddButton => Strings.SettingsSplitTunnelingAddButton;
    public string SplitTunnelingDomainsEmpty => Strings.SettingsSplitTunnelingDomainsEmpty;
    public string SplitTunnelingApplicationsEmpty => Strings.SettingsSplitTunnelingApplicationsEmpty;
    public string KillSwitchTitle => Strings.SettingsKillSwitchTitle;
    public string KillSwitchDescription => Strings.SettingsKillSwitchDescription;
    public string KillSwitchModeTitle => Strings.SettingsKillSwitchModeTitle;
    public string KillSwitchModeDescription => Strings.SettingsKillSwitchModeDescription;
    public string KillSwitchSoftMode => Strings.SettingsKillSwitchSoftMode;
    public string KillSwitchHardMode => Strings.SettingsKillSwitchHardMode;
    public string VpnProtocolTitle => Strings.SettingsVpnProtocolTitle;
    public string VpnProtocolDescription => Strings.SettingsVpnProtocolDescription;
    public string CustomDnsTitle => Strings.SettingsCustomDnsTitle;
    public string CustomDnsDescription => Strings.SettingsCustomDnsDescription;
    public string AutoStartupTitle => Strings.SettingsAutoStartupTitle;
    public string AutoStartupDescription => Strings.SettingsAutoStartupDescription;
    public string StayHiddenTitle => Strings.SettingsStayHiddenTitle;
    public string StayHiddenDescription => Strings.SettingsStayHiddenDescription;
    public string AutoUpdatesTitle => Strings.SettingsAutoUpdatesTitle;
    public string AutoUpdatesDescription => Strings.SettingsAutoUpdatesDescription;
    public string AllowBackgroundProcessesTitle => Strings.SettingsAllowBackgroundProcessesTitle;
    public string AllowBackgroundProcessesDescription => Strings.SettingsAllowBackgroundProcessesDescription;
    public string AppearanceTitle => Strings.SettingsAppearanceTitle;
    public string AppearanceDescription => Strings.SettingsAppearanceDescription;
    public string ShowNotificationsTitle => Strings.SettingsShowNotificationsTitle;
    public string ShowNotificationsDescription => Strings.SettingsShowNotificationsDescription;
    public string SendAppLogsTitle => Strings.SettingsSendAppLogsTitle;
    public string SendAppLogsDescription => Strings.SettingsSendAppLogsDescription;
    public string ResetSettingsTitle => Strings.SettingsResetSettingsTitle;
    public string ResetSettingsDescription => Strings.SettingsResetSettingsDescription;
    public string ResetSettingsButton => Strings.SettingsResetSettingsButton;

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

    public bool IsVpnConnectionSelected => SelectedSection is SettingsSection.VpnConnection;
    public bool IsSecurityAndPrivacySelected => SelectedSection is SettingsSection.SecurityAndPrivacy;
    public bool IsGeneralSelected => SelectedSection is SettingsSection.General;
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
        SplitTunnelingForDomains = false;
        SplitTunnelingForApplications = false;
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
