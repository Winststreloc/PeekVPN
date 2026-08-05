# PeekVPN Service Installation

The background service listens for gRPC calls from the Avalonia client on `http://localhost:50052`.

## Build

Publish the service for your target platform:

```bash
dotnet publish src/PeekVPN.Service/PeekVPN.Service.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -o ./publish/win-x64
```

Supported runtimes: `win-x64`, `linux-x64`, `osx-x64`.

## Install

### Windows (PowerShell as Administrator)

```powershell
.\src\PeekVPN.Service\Installers\install-windows.ps1
```

### Linux (root)

```bash
sudo ./src/PeekVPN.Service/Installers/install-linux.sh
```

### macOS (root)

```bash
sudo ./src/PeekVPN.Service/Installers/install-macos.sh
```

## Development

Run the service directly without installing:

```bash
dotnet run --project src/PeekVPN.Service/PeekVPN.Service.csproj
```

Then start the Avalonia client. The client expects the service at `http://localhost:50052`.
