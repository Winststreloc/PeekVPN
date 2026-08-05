using PeekVPN.App.Localization;
using PeekVPN.App.ViewModels;

namespace PeekVPN.Core.Tests;

public sealed class SettingsPageViewModelTests
{
    [Fact]
    public void Navigation_selects_requested_section_and_updates_active_state()
    {
        var viewModel = new SettingsPageViewModel();

        viewModel.SelectSectionCommand.Execute(SettingsSection.General);

        Assert.Equal(SettingsSection.General, viewModel.SelectedSection);
        Assert.True(viewModel.IsGeneralSelected);
        Assert.False(viewModel.IsVpnConnectionSelected);
        Assert.False(viewModel.IsSecurityAndPrivacySelected);
    }

    [Fact]
    public void Reset_settings_restores_ui_only_defaults()
    {
        var viewModel = new SettingsPageViewModel
        {
            AutoConnect = false,
            AutoConnectUseFastestServer = false,
            AutoConnectServer = Strings.SettingsServerAmsterdam,
            SplitTunneling = true,
            SplitTunnelingForDomains = true,
            SplitTunnelingForApplications = true,
            KillSwitch = false,
            SelectedKillSwitchMode = Strings.SettingsKillSwitchHardMode,
            CustomDns = true,
            AutoStartup = true,
            StayHidden = true,
            AutoUpdates = false,
            AllowBackgroundProcesses = false,
            ShowNotifications = false,
            SendAppLogs = true,
            Appearance = Strings.SettingsAppearanceDark,
            VpnProtocol = Strings.SettingsProtocolOpenVpn
        };

        viewModel.ResetSettingsCommand.Execute(null);

        Assert.True(viewModel.AutoConnect);
        Assert.True(viewModel.AutoConnectUseFastestServer);
        Assert.Equal(Strings.SettingsServerPlaceholder, viewModel.AutoConnectServer);
        Assert.False(viewModel.SplitTunneling);
        Assert.False(viewModel.SplitTunnelingForDomains);
        Assert.False(viewModel.SplitTunnelingForApplications);
        Assert.True(viewModel.KillSwitch);
        Assert.Equal(Strings.SettingsKillSwitchSoftMode, viewModel.SelectedKillSwitchMode);
        Assert.False(viewModel.CustomDns);
        Assert.False(viewModel.AutoStartup);
        Assert.False(viewModel.StayHidden);
        Assert.True(viewModel.AutoUpdates);
        Assert.True(viewModel.AllowBackgroundProcesses);
        Assert.True(viewModel.ShowNotifications);
        Assert.False(viewModel.SendAppLogs);
        Assert.Equal(Strings.SettingsAppearanceLight, viewModel.Appearance);
        Assert.Equal(Strings.SettingsProtocolRecommended, viewModel.VpnProtocol);
    }

    [Fact]
    public void Connection_configuration_remains_available_when_master_toggles_are_off()
    {
        var viewModel = new SettingsPageViewModel
        {
            AutoConnect = false,
            SplitTunneling = false,
            KillSwitch = false
        };

        Assert.True(viewModel.IsAutoConnectTargetEnabled);
        Assert.False(viewModel.IsAutoConnectServerSelectionEnabled);
        Assert.True(viewModel.IsSplitTunnelingOptionsEnabled);
        Assert.True(viewModel.IsKillSwitchModeEnabled);

        viewModel.AutoConnectUseFastestServer = false;

        Assert.True(viewModel.IsAutoConnectTargetEnabled);
        Assert.True(viewModel.IsAutoConnectServerSelectionEnabled);
        Assert.True(viewModel.IsSplitTunnelingOptionsEnabled);
        Assert.True(viewModel.IsKillSwitchModeEnabled);
    }

    [Fact]
    public void Split_tunneling_lists_accept_ui_only_entries()
    {
        var viewModel = new SettingsPageViewModel
        {
            NewSplitTunnelingDomain = "example.com",
            NewSplitTunnelingApplication = "/usr/bin/browser"
        };

        viewModel.AddSplitTunnelingDomainCommand.Execute(null);
        viewModel.AddSplitTunnelingApplicationCommand.Execute(null);

        Assert.Equal(["example.com"], viewModel.SplitTunnelingDomains);
        Assert.Equal(["/usr/bin/browser"], viewModel.SplitTunnelingApplications);
        Assert.True(viewModel.IsSplitTunnelingDomainsEmpty is false);
        Assert.True(viewModel.IsSplitTunnelingApplicationsEmpty is false);
    }
}
