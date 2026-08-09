# OpenClaw Manager Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Replace the current BAT/PowerShell wrapper with a real Windows 10/11 x64 WPF desktop application that installs OpenClaw and manages Gateway, configuration, logs, repair, and uninstall.

**Architecture:** A self-contained .NET 8 WPF MVVM app calls official Node/npm/OpenClaw commands through a safe process runner. Focused services persist ownership, backups, state, and logs. Inno Setup packages the app.

**Tech Stack:** C#; .NET 8; WPF; MVVM; xUnit; System.Diagnostics.Process; Inno Setup; GitHub CLI.

## Global Constraints

- Target Windows 10/11 x64.
- First release is online-only.
- No chat interface, plugin marketplace, channel management, or Edge TTS deployment in v1.
- Runtime user interaction must not depend on BAT, CMD, or PowerShell UI.
- External commands use argument-safe process execution and real exit-code checks.
- API keys/tokens are redacted from UI and file logs.
- Gateway does not open LAN access by default.
- Back up configuration before mutation.
- Uninstall removes only owned or explicitly confirmed resources.
- Shortcuts target the installed manager executable, never a temporary extraction path.
- The current machine has no dotnet command, so provision .NET 8 SDK before building.

## File Map

Create:

- OpenClawManager.sln
- src/OpenClawManager/OpenClawManager.csproj
- src/OpenClawManager/App.xaml and App.xaml.cs
- src/OpenClawManager/Views/MainWindow.xaml
- src/OpenClawManager/Views/Pages/OverviewPage.xaml
- src/OpenClawManager/Views/Pages/InstallRepairPage.xaml
- src/OpenClawManager/Views/Pages/GatewayPage.xaml
- src/OpenClawManager/Views/Pages/ModelConfigPage.xaml
- src/OpenClawManager/Views/Pages/LogsPage.xaml
- src/OpenClawManager/Views/Pages/UninstallPage.xaml
- src/OpenClawManager/ViewModels/MainWindowViewModel.cs
- src/OpenClawManager/ViewModels/OverviewViewModel.cs
- src/OpenClawManager/ViewModels/InstallRepairViewModel.cs
- src/OpenClawManager/ViewModels/GatewayViewModel.cs
- src/OpenClawManager/ViewModels/ModelConfigViewModel.cs
- src/OpenClawManager/ViewModels/LogsViewModel.cs
- src/OpenClawManager/ViewModels/UninstallViewModel.cs
- src/OpenClawManager/Core/Models/CommandResult.cs
- src/OpenClawManager/Core/Models/ProcessRunOptions.cs
- src/OpenClawManager/Core/Models/EnvironmentSnapshot.cs
- src/OpenClawManager/Core/Models/GatewayStatus.cs
- src/OpenClawManager/Core/Models/InstallState.cs
- src/OpenClawManager/Core/Models/InstallStep.cs
- src/OpenClawManager/Core/Models/InstallProgress.cs
- src/OpenClawManager/Core/Models/InstallWorkflowModels.cs
- src/OpenClawManager/Core/Models/ModelProvider.cs
- src/OpenClawManager/Core/Models/NodeModels.cs
- src/OpenClawManager/Core/Models/OpenClawModels.cs
- src/OpenClawManager/Core/Models/ConfigModels.cs
- src/OpenClawManager/Core/Models/UninstallModels.cs
- src/OpenClawManager/Core/Services/*.cs
- src/OpenClawManager/Infrastructure/PathLayout.cs
- src/OpenClawManager/Infrastructure/CommandCatalog.cs
- src/OpenClawManager/Infrastructure/AdminElevation.cs
- src/OpenClawManager/Infrastructure/VersionPolicy.cs
- src/OpenClawManager/Resources/Styles.xaml
- src/OpenClawManager/Resources/Icons.xaml
- tests/OpenClawManager.Tests/OpenClawManager.Tests.csproj
- tests/OpenClawManager.Tests/*Tests.cs
- packaging/OpenClawManager.iss
- packaging/app.ico
- README.md
- .gitignore

Preserve as legacy reference, but do not invoke as the new core workflow:

- setup.bat
- install.ps1
- build-installer.ps1
- 快捷操作.bat
- uninstall.bat
- 使用指南.txt
- plugins/edge-tts/

---

## Task 1: Provision SDK and scaffold

**Files:** OpenClawManager.sln; src/OpenClawManager/OpenClawManager.csproj; tests/OpenClawManager.Tests/OpenClawManager.Tests.csproj; .gitignore; README.md.

- [ ] Install .NET 8 x64 SDK because dotnet is currently missing. Verify with:

~~~powershell
dotnet --info
dotnet --list-sdks
~~~

- [ ] Create the solution:

~~~powershell
dotnet new sln -n OpenClawManager
dotnet new wpf -n OpenClawManager -o src/OpenClawManager --framework net8.0-windows
dotnet new xunit -n OpenClawManager.Tests -o tests/OpenClawManager.Tests --framework net8.0
dotnet sln OpenClawManager.sln add src/OpenClawManager/OpenClawManager.csproj
dotnet sln OpenClawManager.sln add tests/OpenClawManager.Tests/OpenClawManager.Tests.csproj
dotnet add tests/OpenClawManager.Tests/OpenClawManager.Tests.csproj reference src/OpenClawManager/OpenClawManager.csproj
~~~

- [ ] Enable WPF, nullable analysis, implicit usings, and Windows x64 publish settings. Ignore bin, obj, publish, packaging output, local state, logs, secrets, and .superpowers.
- [ ] Build and test:

~~~powershell
dotnet build OpenClawManager.sln
dotnet test OpenClawManager.sln
~~~

- [ ] Commit:

~~~powershell
git add OpenClawManager.sln src tests .gitignore README.md
git commit -m "chore: scaffold OpenClaw Manager"
~~~

## Task 2: Add models, paths, state, and logging

**Files:** Core/Models/*.cs; Infrastructure/PathLayout.cs; Core/Services/IStateStore.cs; JsonStateStore.cs; ILogService.cs; LogService.cs; tests/StateStoreTests.cs.

**Interfaces:**

~~~csharp
public sealed record CommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    TimeSpan Duration,
    bool TimedOut,
    bool Cancelled);

public interface IStateStore
{
    Task<InstallState> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(InstallState state, CancellationToken cancellationToken);
}
~~~

- [ ] Write failing tests for missing-state defaults, state round-trip, and paths under %LOCALAPPDATA%\\OpenClawManager.
- [ ] Implement immutable models for CommandResult, EnvironmentSnapshot, GatewayStatus, InstallState, InstallStep, ModelProvider, and backup metadata.
- [ ] Implement JSON schema versioning and atomic state writes through state.json.tmp followed by replacement.
- [ ] Implement structured file/UI logging with redaction for key, token, secret, password, authorization, and provider secret fields.
- [ ] Run focused tests and commit:

~~~powershell
dotnet test tests/OpenClawManager.Tests --filter FullyQualifiedName~StateStoreTests
git add src/OpenClawManager/Core/Models src/OpenClawManager/Core/Services src/OpenClawManager/Infrastructure tests
git commit -m "feat: add state paths and structured logging"
~~~

## Task 3: Add safe process runner and environment detection

**Files:** Core/Services/IProcessRunner.cs; ProcessRunner.cs; IEnvironmentService.cs; EnvironmentService.cs; Infrastructure/CommandCatalog.cs; VersionPolicy.cs; tests/ProcessRunnerTests.cs; tests/VersionPolicyTests.cs.

**Interfaces:**

~~~csharp
public interface IProcessRunner
{
    Task<CommandResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        ProcessRunOptions options,
        CancellationToken cancellationToken);
}

public interface IEnvironmentService
{
    Task<EnvironmentSnapshot> DetectAsync(CancellationToken cancellationToken);
}
~~~

- [ ] Test non-zero exit, timeout, cancellation, and arguments containing spaces using a fake or dotnet child process.
- [ ] Implement ProcessRunner with ProcessStartInfo.ArgumentList, UseShellExecute=false, redirected stdout/stderr, timeout, cancellation, and deterministic disposal. Do not call cmd.exe for OpenClaw operations.
- [ ] Implement command builders for node, npm, openclaw, where, and only required OS operations.
- [ ] Implement strict semantic version parsing. Do not accept a Node major version alone.
- [ ] Detect Windows version, x64, network, free disk, Node/npm/OpenClaw paths and versions, and Gateway port occupancy.
- [ ] Run tests and commit:

~~~powershell
dotnet test tests/OpenClawManager.Tests --filter "FullyQualifiedName~ProcessRunnerTests|FullyQualifiedName~VersionPolicyTests"
git add src/OpenClawManager/Core/Services src/OpenClawManager/Infrastructure tests
git commit -m "feat: add safe process runner and environment detection"
~~~

## Task 4: Add Node and OpenClaw installers

**Files:** Core/Services/INodeService.cs; NodeService.cs; IOpenClawCliService.cs; OpenClawCliService.cs; Infrastructure/AdminElevation.cs; tests/InstallAdapterTests.cs.

**Interfaces:**

~~~csharp
public interface INodeService
{
    Task<NodeResult> EnsureCompatibleAsync(
        NodeInstallOptions options,
        IProgress<InstallProgress> progress,
        CancellationToken cancellationToken);
}

public interface IOpenClawCliService
{
    Task<OpenClawVersionResult> GetVersionAsync(CancellationToken cancellationToken);
    Task<CommandResult> InstallAsync(string packageSpec, CancellationToken cancellationToken);
    Task<CommandResult> ValidateConfigAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken);
}
~~~

- [ ] Write fake ProcessRunner tests proving failed commands cannot be reported as success.
- [ ] Reuse a compatible existing Node. If needed, elevate only the installer operation, refresh PATH, and verify node -v and npm -v before recording ownership.
- [ ] Run npm install -g openclaw@latest through ProcessRunner and verify openclaw --version.
- [ ] Classify network failures as retryable, invalid version/config as actionable, and access denied as elevation-required.
- [ ] Run tests and commit:

~~~powershell
dotnet test tests/OpenClawManager.Tests --filter FullyQualifiedName~InstallAdapterTests
git add src/OpenClawManager/Core/Services src/OpenClawManager/Infrastructure tests
git commit -m "feat: add Node and OpenClaw installation adapters"
~~~

## Task 5: Add configuration, credentials, and backups

**Files:** Core/Services/IConfigService.cs; ConfigService.cs; ICredentialService.cs; CredentialService.cs; ModelProviderCatalog.cs; tests/ConfigServiceTests.cs.

**Interfaces:**

~~~csharp
public interface IConfigService
{
    Task<string> BackupAsync(CancellationToken cancellationToken);
    Task<CommandResult> ConfigureModelAsync(ModelConfiguration configuration, CancellationToken cancellationToken);
    Task<CommandResult> ValidateAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ConfigBackup>> ListBackupsAsync(CancellationToken cancellationToken);
    Task RestoreAsync(string backupPath, CancellationToken cancellationToken);
}
~~~

- [ ] Test backup contents, selective restore, and absence of API keys/tokens in logs.
- [ ] Back up %USERPROFILE%\\.openclaw to a timestamped directory with a manifest and file hashes before mutation.
- [ ] Add provider metadata for OpenAI, Anthropic, Google, DeepSeek, OpenRouter, and Custom OpenAI-compatible.
- [ ] Use official openclaw onboard --non-interactive or openclaw config set with argument-safe builders. Use secret references where supported. Validate and restore on validation failure.
- [ ] Run tests and commit:

~~~powershell
dotnet test tests/OpenClawManager.Tests --filter FullyQualifiedName~ConfigServiceTests
git add src/OpenClawManager/Core/Services tests
git commit -m "feat: add config backup and secure model setup"
~~~

## Task 6: Add Gateway lifecycle and installation state machine

**Files:** Core/Services/IGatewayService.cs; GatewayService.cs; IInstallCoordinator.cs; InstallCoordinator.cs; tests/InstallCoordinatorTests.cs.

**Interfaces:**

~~~csharp
public interface IGatewayService
{
    Task<GatewayStatus> GetStatusAsync(CancellationToken cancellationToken);
    Task<CommandResult> InstallAsync(CancellationToken cancellationToken);
    Task<CommandResult> StartAsync(CancellationToken cancellationToken);
    Task<CommandResult> StopAsync(CancellationToken cancellationToken);
    Task<CommandResult> RestartAsync(CancellationToken cancellationToken);
    Task<CommandResult> UninstallAsync(CancellationToken cancellationToken);
}

public interface IInstallCoordinator
{
    InstallWorkflowState Current { get; }
    Task<InstallWorkflowResult> RunAsync(InstallOptions options, IProgress<InstallProgress> progress, CancellationToken cancellationToken);
    Task<InstallWorkflowResult> RepairAsync(IProgress<InstallProgress> progress, CancellationToken cancellationToken);
}
~~~

- [ ] Test successful transitions, failure halting, retry, cancellation, and restore after config failure using fakes.
- [ ] Map install, start, stop, restart, status, and uninstall to official OpenClaw commands. Parse status and treat non-zero as failure.
- [ ] Persist each successful InstallStep and emit progress with step name, percentage, summary, and log reference.
- [ ] Verify Gateway process status, port response, and OpenClaw health before reporting completion.
- [ ] Run tests and commit:

~~~powershell
dotnet test tests/OpenClawManager.Tests --filter FullyQualifiedName~InstallCoordinatorTests
git add src/OpenClawManager/Core/Services tests
git commit -m "feat: add Gateway lifecycle and install workflow"
~~~

## Task 7: Build WPF shell and Fluent UI

**Files:** App.xaml; App.xaml.cs; Views/MainWindow.xaml; Views/Pages/*.xaml; ViewModels/*.cs; Resources/Styles.xaml; Resources/Icons.xaml.

- [ ] Build the A-layout shell: left navigation, title bar, content host, status footer, and global resources. Support 1024x700 and above.
- [ ] Build Overview with OpenClaw, Node, Gateway, model, port, runtime cards and actions.
- [ ] Build Install/Repair with six steps, progress, current action, retry, cancel, and expandable detail log.
- [ ] Build Gateway and Logs pages with service actions and redacted live logs.
- [ ] Build ModelConfig with masked secret input, provider selection, model input/list, and validation status.
- [ ] Build Uninstall with backup and cleanup choices.
- [ ] Use Fluent vector resources or Segoe Fluent Icons; add text labels, keyboard focus, disabled states, and tooltips.
- [ ] Run:

~~~powershell
dotnet build OpenClawManager.sln
dotnet run --project src/OpenClawManager/OpenClawManager.csproj
~~~

- [ ] Verify all pages when OpenClaw is absent and commit:

~~~powershell
git add src/OpenClawManager
git commit -m "feat: add Fluent WPF manager interface"
~~~

## Task 8: Add uninstall, shortcuts, and ownership

**Files:** Core/Services/IUninstallService.cs; UninstallService.cs; PathLayout.cs; JsonStateStore.cs; tests/UninstallServiceTests.cs.

**Interface:**

~~~csharp
public interface IUninstallService
{
    Task<UninstallPreview> PreviewAsync(CancellationToken cancellationToken);
    Task<UninstallResult> ExecuteAsync(UninstallOptions options, IProgress<InstallProgress> progress, CancellationToken cancellationToken);
}
~~~

- [ ] Test that only InstallState-owned resources are removed, configuration can be preserved, and arbitrary API environment variables are untouched.
- [ ] Implement preview listing Gateway, OpenClaw, Node, shortcuts, manager data, and configuration as will remove or will preserve.
- [ ] Clean in order: stop Gateway, uninstall Gateway, optionally uninstall owned OpenClaw/Node, remove owned shortcuts, handle configuration by explicit choice, remove manager data.
- [ ] Create shortcuts pointing to installed OpenClawManager.exe, never a temporary BAT. Store shortcut paths in state.
- [ ] Run tests and commit:

~~~powershell
dotnet test tests/OpenClawManager.Tests --filter FullyQualifiedName~UninstallServiceTests
git add src/OpenClawManager tests
git commit -m "feat: add owned-resource uninstall flow"
~~~

## Task 9: Package and validate

**Files:** packaging/OpenClawManager.iss; packaging/app.ico; README.md; docs/testing/clean-windows-smoke-test.md.

- [ ] Provision Inno Setup because iscc is currently missing. Verify with iscc /?.
- [ ] Publish self-contained x64:

~~~powershell
dotnet publish src/OpenClawManager/OpenClawManager.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o packaging/publish
~~~

- [ ] Write Inno Setup definition installing under Program Files\\OpenClaw Manager, adding Start Menu/Desktop shortcuts, version metadata, and an uninstaller. Do not bundle user config or secrets.
- [ ] Build:

~~~powershell
iscc packaging/OpenClawManager.iss
Get-Item packaging/output/OpenClawManagerSetup.exe | Select-Object FullName,Length
~~~

- [ ] Run:

~~~powershell
dotnet format OpenClawManager.sln --verify-no-changes
dotnet test OpenClawManager.sln -c Release
~~~

- [ ] Document a clean Windows 10/11 x64 test covering first install, compatible/incompatible Node, failed download, port conflict, Gateway restart, manager restart, backup restore, and uninstall with/without config.
- [ ] Commit packaging and docs.

## Task 10: Create and push GitHub repository with gh

**Produces:** https://github.com/nanfangns/openclaw-manager with local origin and pushed main branch.

- [ ] Verify:

~~~powershell
git status --short
dotnet test OpenClawManager.sln -c Release
gh auth status
gh repo view nanfangns/openclaw-manager
~~~

If the repo does not exist, create it. If it exists, inspect it before changing remotes.

- [ ] Create final local history:

~~~powershell
git add .
git commit -m "feat: create OpenClaw Manager desktop application"
git branch -M main
~~~

- [ ] Create a private repository by default because visibility was not specified:

~~~powershell
gh repo create nanfangns/openclaw-manager --private --source . --remote origin --push --description "Windows installer and manager for OpenClaw"
~~~

Do not publish secrets, local state, build output, API keys, or .superpowers files. Public visibility requires an explicit later request.

- [ ] Verify:

~~~powershell
gh repo view nanfangns/openclaw-manager --web
git remote -v
git status --short
~~~

Expected: origin points to the new repo, main is pushed, and the worktree is clean.

## Final Verification Checklist

- [ ] dotnet build OpenClawManager.sln passes.
- [ ] dotnet test OpenClawManager.sln -c Release passes.
- [ ] Runtime does not invoke the legacy scripts.
- [ ] OpenClaw operations do not use cmd /c.
- [ ] Failed commands cannot display success.
- [ ] Configuration is backed up before mutation.
- [ ] API keys do not appear in UI or file logs.
- [ ] Gateway status is checked after start/restart.
- [ ] Default setup does not open LAN access.
- [ ] Shortcuts target the installed executable.
- [ ] Uninstall preserves configuration when requested.
- [ ] Clean Windows smoke test is documented and run.
- [ ] GitHub repository is created and pushed through gh.
