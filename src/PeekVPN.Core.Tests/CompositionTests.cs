using Microsoft.Extensions.DependencyInjection;
using PeekVPN.App.DependencyInjection;
using PeekVPN.App.ViewModels;

namespace PeekVPN.Core.Tests;

public sealed class CompositionTests
{
    [Fact]
    public void App_composition_resolves_shell_and_all_workspace_features()
    {
        var services = new ServiceCollection();
        services.AddPeekVpnApp();
        using var provider = services.BuildServiceProvider();

        var shell = provider.GetRequiredService<ShellViewModel>();

        Assert.NotNull(shell.Workspace.ServerBrowser);
        Assert.NotNull(shell.Workspace.ConnectionPanel);
        Assert.NotNull(shell.Workspace.StatsSummary);
        Assert.NotNull(shell.Workspace.Map);
        Assert.NotNull(shell.Workspace.FeatureCards);
        Assert.NotNull(shell.Statistics);
        Assert.NotNull(shell.Profile);
        Assert.NotNull(shell.Settings);

        shell.Workspace.Dispose();
    }

    [Fact]
    public void Shell_navigation_switches_page_and_active_state()
    {
        var services = new ServiceCollection();
        services.AddPeekVpnApp();
        using var provider = services.BuildServiceProvider();

        var shell = provider.GetRequiredService<ShellViewModel>();

        shell.NavigateToCommand.Execute(ShellPage.Profile);

        Assert.Equal(ShellPage.Profile, shell.SelectedPage);
        Assert.Same(shell.Profile, shell.CurrentPage);
        Assert.True(shell.IsProfileActive);
        Assert.False(shell.IsMapActive);

        shell.Workspace.Dispose();
    }
}
