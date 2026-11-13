# Stops and removes the GrokBotRoute service and its Program Files binary.
# Does not restore the former scheduled task.

[CmdletBinding(SupportsShouldProcess = $true)]
param()

$ErrorActionPreference = 'Stop'

$serviceName = 'GrokBotRoute'
$installDir = 'C:\Program Files\GrokBotRoute'

if (-not $PSCmdlet.ShouldProcess($serviceName, 'remove the Windows service and Program Files binary')) {
    Write-Output "WhatIf: stop and delete service $serviceName, remove $installDir"
    return
}

$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$principal = [Security.Principal.WindowsPrincipal]::new($identity)
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Uninstall-GrokBotRouteService must run in an elevated PowerShell.'
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($null -ne $existing) {
    if ($existing.Status -eq 'Running') {
        Stop-Service -Name $serviceName -Force
        $existing.WaitForStatus('Stopped', [timespan]::FromSeconds(30))
    }

    $deleted = & sc.exe delete $serviceName
    if ($LASTEXITCODE -ne 0) {
        throw "sc.exe delete exited $LASTEXITCODE`: $($deleted -join ' ')"
    }
}

if (Test-Path -LiteralPath $installDir) {
    Remove-Item -LiteralPath $installDir -Recurse -Force
}

Write-Output "Removed $serviceName"
