# PeekVPN

Desktop VPN client for **Windows** and **Linux**, built with Avalonia.

## Solution layout

```
src/
  PeekVPN.Contracts/       API DTOs and IVpnApiClient
  PeekVPN.Core/            Session state machine and application services
  PeekVPN.Infrastructure/  Mock (and future real) API adapters
  PeekVPN.App/             Avalonia UI, styles, localization, DI composition root
```

## Run

```bash
dotnet run --project src/PeekVPN.App/PeekVPN.App.csproj
```

Publish targets: `win-x64`, `linux-x64`.
