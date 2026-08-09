# OpenClaw Manager

Windows 10/11 x64 desktop installer and manager for OpenClaw.

## Scope

The application installs Node.js/OpenClaw, configures a model provider, manages the OpenClaw Gateway, shows logs, backs up configuration, and provides a safe uninstall flow. It does not provide a chat UI or plugin marketplace.

The BAT and PowerShell files at the repository root are legacy reference material. The desktop application does not use them as its runtime workflow.

## Build

The development build uses the .NET 8 SDK. Run `dotnet restore OpenClawManager.sln`, `dotnet build OpenClawManager.sln`, and `dotnet test OpenClawManager.sln`.

## Publish

Run `dotnet publish src/OpenClawManager/OpenClawManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o packaging/publish`.

Inno Setup packages the published files into OpenClawManagerSetup.exe.

## Runtime data

Application logs, state, and backups are stored below `%LOCALAPPDATA%\OpenClawManager`. OpenClaw user data remains under `%USERPROFILE%\.openclaw` unless the user explicitly chooses to remove it during uninstall.
