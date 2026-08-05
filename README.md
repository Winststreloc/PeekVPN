# PeekVPN

Desktop VPN client for **Windows** and **Linux**, built with Avalonia.

## Solution layout

```
src/
  PeekVPN.Contracts/       API DTOs and IVpnApiClient
  PeekVPN.Core/            Session state machine and application services
  PeekVPN.Infrastructure/  Mock (and future real) API adapters
  PeekVPN.App/             Avalonia UI, styles, localization, DI composition root
  PeekVPN.Service/         Background gRPC VPN service
```

## Logging

Both executables configure Serilog centrally through `PeekVPN.Core/Logging/PeekVpnLogging.cs`.
Structured fields and `ILogger.BeginScope()` fields are written on every line. Daily text files are
retained for up to 14 days per file series:

- `application-YYYYMMDD.log`: `PeekVPN.App` source categories.
- `service-YYYYMMDD.log`: `PeekVPN.Core`, `PeekVPN.Infrastructure`, and background-service categories.

The log directory is `LocalApplicationData/PeekVPN/logs`:

- Windows: `%LOCALAPPDATA%\PeekVPN\logs`
- Linux: `~/.local/share/PeekVPN/logs`
- macOS: `~/Library/Application Support/PeekVPN/logs`

The Debug build also writes to the console. Release builds write only to these files. Logging is
flushed when either executable shuts down.

## Run

```bash
dotnet run --project src/PeekVPN.App/PeekVPN.App.csproj
```

Publish targets: `win-x64`, `linux-x64`.
