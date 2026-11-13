# Grok Bot route

This directory contains the GrokBotRoute Windows service, which starts the Grok Bot desktop application in the logged-on user's session so local egress remains available to bots that route traffic through this computer.

## Contents

| Path | Role |
|---|---|
| `src/GrokBotRoute/` | .NET 9 worker that polls for the desktop application and starts it in the interactive session |
| `tests/GrokBotRoute.Tests/` | xUnit tests for the start decision and session selection |
| `Install-GrokBotRouteService.ps1` | Publishes the self-contained binary and registers the Windows service |
| `Uninstall-GrokBotRouteService.ps1` | Removes the service and `C:\Program Files\GrokBotRoute` |
| `nuget.config` | Restricts package restore to nuget.org |

## Operation

The desktop setting **Route traffic through this computer** directs bot web traffic through this machine. That route requires the Grok Bot process. The service polls every 30 seconds. When `Grok Bot.exe` is absent and an interactive session exists, the service starts the executable with `CreateProcessAsUser` on `winsta0\default`. The process runs as the logged-on user. The service process runs as LocalSystem in Session 0 and does not host the desktop application.

The installed binary is `C:\Program Files\GrokBotRoute\GrokBotRoute.exe`. The service name is `GrokBotRoute`. The start type is Automatic. A service failure restarts the process after 5 seconds, up to three times, and the failure count resets after 24 hours.

## Interactive Logon

Windows creates an interactive session only through Winlogon: a console logon, a Remote Desktop logon, or automatic logon. A service cannot create that session. `LogonUser` and `CreateProcessWithLogonW` produce a token or a process. They do not create the console desktop. Retrieval of a credential from OpenBao does not create an interactive logon.

After a reboot that stops at the login screen, `GrokBotRoute` reports Running and the Grok Bot process is absent. Local egress is unavailable until an interactive logon completes. The following poll starts Grok Bot in that session.

A locked workstation remains an interactive session. When Grok Bot exits during that session, the service starts it again. The login screen after reboot is not an interactive session.

## Automatic Logon

Automatic logon is a Winlogon configuration. This repository does not enable it. When the computer must present a desktop with no operator present, Windows automatic logon supplies the session this service requires.

- [Configure Windows to automate logon](https://learn.microsoft.com/en-us/troubleshoot/windows-server/user-profiles-and-logon/turn-on-automatic-logon) defines `AutoAdminLogon` and states that physical access to the computer grants use of that account.
- [Sysinternals Autologon](https://learn.microsoft.com/en-us/sysinternals/downloads/autologon) stores the credential as an LSA secret. The registry procedure in the article above stores `DefaultPassword` in plain text. Running `autologon.exe`, entering the account, and choosing Enable activates autologon. The Shift key, held during startup, skips autologon for that boot.

After that logon, the service starts Grok Bot within the 30-second poll interval.

## Installation

Build, test, and publish run inside a Podman container (`mcr.microsoft.com/dotnet/sdk:9.0` on the `podman-uosserver` WSL distro). NuGet packages are stored in the `nuget-cache` volume. The host receives the self-contained `win-x64` binary.

1. An elevated PowerShell session is required.
2. Execute the installer.

```powershell
pwsh -NoProfile -File .\Install-GrokBotRouteService.ps1
```

The installer publishes the binary, copies it to `C:\Program Files\GrokBotRoute\GrokBotRoute.exe`, registers the service, and unregisters the scheduled task `\Grok Bot\Keep local route` when that task is present.

Removal of the service and the Program Files directory:

```powershell
pwsh -NoProfile -File .\Uninstall-GrokBotRouteService.ps1
```

`-WhatIf` on either script reports the planned actions and leaves the machine unchanged.

## Tests

```text
wsl -d podman-uosserver -e bash -lc 'podman run --rm --cpus=4 --memory=4g --pids-limit=512 -v /mnt/d/Cursor/windows-optimizations/grok-bot-route:/work:Z -v nuget-cache:/root/.nuget/packages -w /work mcr.microsoft.com/dotnet/sdk:9.0 dotnet test GrokBotRoute.sln -c Release'
```

The rootless Podman runtime directory `/run/user/1000` must exist and be writable by the Podman user. A non-interactive `wsl` launch with no logind session creates it as follows:

```text
sudo mkdir -p /run/user/1000 && sudo chown "$USER:$USER" /run/user/1000 && sudo chmod 700 /run/user/1000
```
