# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

PeekVPN is a desktop VPN client (Windows/Linux) built with Avalonia. The UI talks to a separate
background gRPC service that owns the actual VPN connection/tunnel lifecycle — the desktop app never
touches the network stack directly.

Solution layout (`PeekVPN.slnx`):

```
src/
  PeekVPN.Contracts/       Proto file, gRPC-generated code, and shared DTOs (IVpnApiClient, WireGuardConfig, etc.)
  PeekVPN.Core/            Session state machine, connection orchestrator, and platform-agnostic VPN abstractions
  PeekVPN.Infrastructure/  Concrete adapters: gRPC client, WireGuard (Linux), mock HTTP API
  PeekVPN.App/             Avalonia UI (views, view models, styles, localization) — the DI composition root for the desktop process
  PeekVPN.Service/         ASP.NET Core host exposing PeekVPN.Core over gRPC (the background service)
  PeekVPN.Core.Tests/      xUnit tests for Core/App logic and gRPC integration
```

## Commands

```bash
# Build everything
dotnet build

# Run the desktop app (starts a client that expects the service at http://localhost:50052)
dotnet run --project src/PeekVPN.App/PeekVPN.App.csproj

# Run the background service (must be running for the app to connect/list servers)
dotnet run --project src/PeekVPN.Service/PeekVPN.Service.csproj

# Run all tests
dotnet test src/PeekVPN.Core.Tests/PeekVPN.Core.Tests.csproj

# Run a single test
dotnet test src/PeekVPN.Core.Tests/PeekVPN.Core.Tests.csproj --filter "FullyQualifiedName~ServerBrowserSearchTests"

# Publish (win-x64 / linux-x64 supported for both App and Service)
dotnet publish src/PeekVPN.Service/PeekVPN.Service.csproj -c Release -r win-x64 --self-contained true -o ./publish/win-x64
```

`GrpcIntegrationTests` connects to a live service on `localhost:50052` (override with
`PEEKVPN_TEST_PORT`) — start `PeekVPN.Service` before running the full test suite, or those tests fail
with connection errors rather than being skipped.

## Architecture

### Process split: App (client) vs Service (host)

The state machine and connection logic live once, in `PeekVPN.Core`, but are wired up differently per
process via `AddCore()` / `AddInfrastructure()` extension methods:

- **`PeekVPN.Service`** (`Program.cs`) calls `AddCore()` + `AddInfrastructure()` directly, so
  `IVpnSession`/`IServerCatalog` resolve to the real in-process implementations
  (`VpnSession`, `ServerCatalog`) that drive actual WireGuard connections. It hosts `VpnGrpcService`,
  a thin gRPC wrapper that maps proto messages to/from `PeekVPN.Core` types and delegates everything to
  `IVpnSession`/`IServerCatalog`.
- **`PeekVPN.App`** (`AddPeekVpnApp()`) calls `AddCore()` + `AddInfrastructure()` too (for DI
  completeness/tests) but then calls `AddGrpcClient(serviceUrl)`, which re-registers `IVpnSession` →
  `GrpcVpnSession` and `IServerCatalog` → `GrpcServerCatalog`. Since DI resolves the last registration,
  the app always ends up talking to the service over gRPC — it never runs a real tunnel in-process.
  The service URL comes from `PEEKVPN_SERVICE_URL` (default `http://localhost:50052`).
- `GrpcVpnSession` mirrors server-pushed state via the `SubscribeStatus` streaming RPC and exposes it
  through the same `StateChanged` event/`Snapshot` shape as the real `VpnSession`, so view models don't
  know or care which side of the gRPC boundary they're on.

The gRPC contract (`src/PeekVPN.Contracts/Protos/peekvpn.proto`) is the seam between the two processes;
changing it requires updating both `VpnGrpcService` (server mapping) and `GrpcVpnSession`/
`GrpcServerCatalog` (client mapping).

### VPN connection pipeline (PeekVPN.Core / PeekVPN.Infrastructure)

`VpnSession` (state machine: Disconnected → Connecting → Connected/Paused → Disconnecting) delegates all
actual tunnel work to `IVpnConnectionOrchestrator` (`VpnConnectionOrchestrator`), which is "the only
component the session state machine talks to for actual tunnel work":

1. Orchestrator picks an `IVpnConnectionFactory` whose `CanHandle(protocol)` matches the request
   (currently `WireGuardConnectionFactory` for `"wireguard"`).
2. The factory creates a protocol-specific `IVpnConnection` (`WireGuardConnection`), bound to an
   `IPlatformNetworkServices` instance for the current OS.
3. `IPlatformNetworkServices` (from `PlatformNetworkServicesFactory`) supplies OS-specific
   `ITunnelAdapter`/`IRoutingManager`/`IFirewallManager`/`IDnsManager` implementations. **Only Linux is
   implemented** (`LinuxPlatformNetworkServices` + friends, shelling out to `ip`/`wg` via
   `ShellHelper`); Windows/macOS throw `PlatformNotSupportedException`.
4. `WireGuardConnection.EstablishAsync` does the full wg-quick-equivalent sequence by hand: create
   interface → `wg setconf` → assign addresses → bring interface up → add routes (including a host
   route to the endpoint to avoid a routing loop) → set DNS → optionally enable the kill switch
   firewall rules.

`VpnSession` fetches WireGuard credentials from `IVpnApiClient` (currently `MockVpnHttpService`, an
in-memory fake server catalog + config generator standing in for a real control-plane API) only when a
connect request doesn't already carry credentials.

Session state is exposed as an immutable `VpnSessionSnapshot` (state, active server id, last error);
every mutation goes through `Volatile.Write` + a `StateChanged` event under an internal `SemaphoreSlim`
gate, so concurrent connect/disconnect/cancel calls are serialized rather than racing.

### Avalonia app (PeekVPN.App)

- MVVM via CommunityToolkit.Mvvm (`[ObservableProperty]`, `[RelayCommand]`); `ViewLocator` maps view
  models to views by naming convention.
- `ShellViewModel` is the top-level composition: it hosts `WorkspaceViewModel` (server browser,
  connection panel, stats summary, world map, feature cards) plus the Statistics/Profile/Settings
  pages, and switches between them via `SelectedPage`.
- `WorkspaceViewModel` wires cross-view-model behavior explicitly (e.g. syncing
  `ConnectionPanel.SelectedServerId` from `ServerBrowser.SelectedServer` via a `PropertyChanged`
  subscription) rather than through a mediator/message bus.
- The world map (`Maps/WorldMapProjection.cs`, `Maps/MapViewportTransform.cs`,
  `Controls/InteractiveMapControl.cs`) implements its own lat/long-to-pixel projection and pan/zoom
  transform — there's no external mapping library.
- Localization goes through the generated `Localization/Strings` resource class (`Strings.resx` +
  `Strings.Designer.cs`), not hardcoded UI strings.

### Logging

Both `PeekVPN.App` and `PeekVPN.Service` configure Serilog centrally through
`PeekVPN.Core/Logging/PeekVpnLogging.cs`. Daily rolling files go to
`LocalApplicationData/PeekVPN/logs` (`application-*.log` for App categories, `service-*.log` for
Core/Infrastructure/service categories, 14-day retention); Debug builds also log to console.
