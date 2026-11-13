# Publishes GrokBotRoute in a Podman SDK container, then registers the LocalSystem service.
# Re-run this script to replace the binary. Undo: Uninstall-GrokBotRouteService.ps1.

[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'

$serviceName = 'GrokBotRoute'
$installDir = 'C:\Program Files\GrokBotRoute'
$binary = Join-Path $installDir 'GrokBotRoute.exe'
$taskPath = '\Grok Bot\'
$taskName = 'Keep local route'

if (-not $PSCmdlet.ShouldProcess($serviceName, 'publish the self-contained binary and register the Windows service')) {
    Write-Output "WhatIf: container publish, copy to $installDir, service $serviceName, unregister ${taskPath}${taskName}"
    return
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Install-GrokBotRouteService must run in an elevated PowerShell.'
}

$bash = 'sudo -n mkdir -p /run/user/1000 && sudo -n chown user:user /run/user/1000 && sudo -n chmod 700 /run/user/1000 && podman run --rm --cpus=4 --memory=4g --pids-limit=512 -v /mnt/d/Cursor/windows-optimizations/grok-bot-tunnel-service:/work:Z -v nuget-cache:/root/.nuget/packages -w /work mcr.microsoft.com/dotnet/sdk:9.0 dotnet publish src/GrokBotRoute/GrokBotRoute.csproj -c Release -r win-x64 --self-contained true -o /work/publish -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true'
& wsl.exe -d podman-uosserver -e bash -lc $bash
if ($LASTEXITCODE -ne 0) {
    throw "container publish exited $LASTEXITCODE"
}

$published = Join-Path $PSScriptRoot 'publish\GrokBotRoute.exe'
if (-not (Test-Path -LiteralPath $published -PathType Leaf)) {
    throw "Published binary was not created at $published"
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existing -and $existing.Status -eq 'Running') {
    Stop-Service -Name $serviceName -Force
    $existing.WaitForStatus('Stopped', [timespan]::FromSeconds(30))
}

if (-not (Test-Path -LiteralPath $installDir)) {
    $null = New-Item -ItemType Directory -Path $installDir
}

Copy-Item -Path (Join-Path $PSScriptRoot 'publish\*') -Destination $installDir -Force

if ($null -eq $existing) {
    New-Service `
        -Name $serviceName `
        -BinaryPathName $binary `
        -DisplayName 'Grok Bot route' `
        -StartupType Automatic `
        -Description 'Keep Grok Bot running in the interactive session so local egress routing stays available.'
}
else {
    $image = '"{0}"' -f $binary
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" -Name ImagePath -Value $image
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" -Name Start -Value 2
}

$failure = & sc.exe failure $serviceName reset= 86400 actions= restart/5000/restart/5000/restart/5000
if ($LASTEXITCODE -ne 0) {
    throw "sc.exe failure exited $LASTEXITCODE`: $($failure -join ' ')"
}

$task = Get-ScheduledTask -TaskPath $taskPath -TaskName $taskName -ErrorAction SilentlyContinue
if ($null -ne $task) {
    Unregister-ScheduledTask -TaskPath $taskPath -TaskName $taskName -Confirm:$false
    Write-Output "Unregistered ${taskPath}${taskName}"
}

Start-Service -Name $serviceName
Write-Output "Installed $binary and started $serviceName"
