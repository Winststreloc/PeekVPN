#Requires -RunAsAdministrator

$serviceName = "PeekVPN"
$displayName = "PeekVPN Service"
$installDir = "C:\Program Files\PeekVPN"
$exePath = Join-Path $installDir "PeekVPN.Service.exe"

Write-Host "Installing PeekVPN service to $installDir..."
if (!(Test-Path $installDir)) {
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null
}
Copy-Item -Path ".\publish\win-x64\*" -Destination $installDir -Recurse -Force

$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Removing existing service..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName | Out-Null
    Start-Sleep -Seconds 2
}

Write-Host "Creating Windows service..."
sc.exe create $serviceName binPath= "$exePath" start= auto displayName= "$displayName" | Out-Null
sc.exe start $serviceName | Out-Null

Write-Host "PeekVPN service installed and started."
Write-Host "Check status: Get-Service -Name $serviceName"
