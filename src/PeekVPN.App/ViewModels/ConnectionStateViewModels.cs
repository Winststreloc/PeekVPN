using CommunityToolkit.Mvvm.Input;
using PeekVPN.App.Localization;
using PeekVPN.App.Services;
using PeekVPN.Core.Abstractions;
using PeekVPN.Core.State;

namespace PeekVPN.App.ViewModels;

/// <summary>
/// Shared presentation and session-command surface for a connection state.
/// Connection transitions remain owned by <see cref="IVpnSession"/>.
/// </summary>
public abstract class ConnectionStateViewModelBase(
    IVpnSession session,
    VpnSessionSnapshot snapshot,
    ServerDisplayMetadata? server) : ViewModelBase
{
    protected IVpnSession Session { get; } = session;

    protected ServerDisplayMetadata? Server { get; } = server;

    public VpnConnectionState State => snapshot.State;

    public string? LastError => snapshot.LastError;

    public virtual Uri? FlagUri => Server?.FlagUri;

    public virtual string Headline => Server is null
        ? DefaultHeadline
        : string.Format(Strings.LocationFormat, Server.City, Server.Country);

    public abstract string StatusLabel { get; }

    protected abstract string DefaultHeadline { get; }
}

public sealed partial class DisconnectedStateViewModel(
    IVpnSession session,
    VpnSessionSnapshot snapshot,
    ServerDisplayMetadata? server)
    : ConnectionStateViewModelBase(session, snapshot, server)
{
    public override string StatusLabel => Strings.StatusNotSecured;

    protected override string DefaultHeadline => Strings.HeadlineUnprotected;

    public override Uri? FlagUri => null;

    public override string Headline => DefaultHeadline;

    public string ConnectText => Strings.CtaQuickConnect;

    public bool CanConnect => Server is not null;

    [RelayCommand(AllowConcurrentExecutions = true, CanExecute = nameof(CanConnect))]
    private Task ConnectAsync() => Session.ConnectAsync(Server!.Id);
}

public sealed partial class ConnectingStateViewModel(
    IVpnSession session,
    VpnSessionSnapshot snapshot,
    ServerDisplayMetadata? server)
    : ConnectionStateViewModelBase(session, snapshot, server)
{
    public override string StatusLabel => Strings.StatusConnecting;

    protected override string DefaultHeadline => Strings.HeadlineConnecting;

    public string CancelText => Strings.CtaCancel;

    [RelayCommand]
    private void Cancel() => Session.CancelConnect();
}

public sealed partial class ConnectedStateViewModel(
    IVpnSession session,
    VpnSessionSnapshot snapshot,
    ServerDisplayMetadata? server)
    : ConnectionStateViewModelBase(session, snapshot, server)
{
    public override string StatusLabel => Strings.StatusSecured;

    protected override string DefaultHeadline => Strings.HeadlineProtected;

    public string DisconnectText => Strings.CtaDisconnect;

    public string PauseText => Strings.CtaPause;

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task DisconnectAsync() => Session.DisconnectAsync();

    [RelayCommand]
    private void Pause() => Session.Pause();
}

public sealed partial class PausedStateViewModel(
    IVpnSession session,
    VpnSessionSnapshot snapshot,
    ServerDisplayMetadata? server)
    : ConnectionStateViewModelBase(session, snapshot, server)
{
    public override string StatusLabel => Strings.StatusPaused;

    protected override string DefaultHeadline => Strings.HeadlinePaused;

    public string DisconnectText => Strings.CtaDisconnect;

    public string ResumeText => Strings.CtaResume;

    [RelayCommand(AllowConcurrentExecutions = true)]
    private Task DisconnectAsync() => Session.DisconnectAsync();

    [RelayCommand]
    private void Resume() => Session.Resume();
}

public sealed class DisconnectingStateViewModel(
    IVpnSession session,
    VpnSessionSnapshot snapshot,
    ServerDisplayMetadata? server)
    : ConnectionStateViewModelBase(session, snapshot, server)
{
    public override string StatusLabel => Strings.StatusDisconnecting;

    protected override string DefaultHeadline => Strings.HeadlineDisconnecting;
}
