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

        shell.Workspace.Dispose();
    }
}
