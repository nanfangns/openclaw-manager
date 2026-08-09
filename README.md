# OpenClaw Manager

[![CI](https://github.com/nanfangns/openclaw-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/nanfangns/openclaw-manager/actions/workflows/ci.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4)](https://www.microsoft.com/windows/)

一个面向 Windows 10/11 x64 的 OpenClaw 图形化安装器和本机管理后台。

<p align="center">
  <img src="src/OpenClawManager/Assets/OpenClawManagerLogo.png" alt="OpenClaw Manager logo" width="180">
</p>

它负责安装运行环境、配置模型、管理 Gateway、查看日志、备份恢复配置和安全卸载。它不是聊天客户端，也不依赖 BAT 或 PowerShell 作为运行时界面。

## 功能

- **安装**：检测 Node.js 版本；必要时下载并校验官方 x64 MSI；安装并验证 OpenClaw CLI。
- **模型配置**：支持 OpenAI、Anthropic、Google Gemini、DeepSeek、OpenRouter 和自定义 OpenAI-compatible 服务。
- **Gateway 管理**：安装、启动、停止、重启、状态检查和健康检查。
- **配置管理**：在修改前备份 `%USERPROFILE%\.openclaw`，支持带 SHA-256 清单的备份恢复。
- **日志**：记录安装和服务操作，API Key、Token、Secret、Password 等敏感值自动脱敏。
- **安全卸载**：只清理由管理器记录为自有的资源；配置、工作区、Node.js 和管理器数据均需要明确选择。
- **本地化默认值**：不默认开放局域网访问，不默认修改用户环境变量，不默认删除已有配置。

## 当前版本边界

当前 v1 版本暂不包含：

- 聊天界面
- 插件市场
- Channel / 消息渠道管理
- Edge TTS 自动部署
- 默认 LAN 访问

## 快速开始

### 直接安装

在 Windows 10/11 x64 上运行本地生成的安装包：

```text
packaging/output/OpenClawManagerSetup.exe
```

安装程序会创建 Start Menu 快捷方式，并可选创建桌面快捷方式。安装完成后，从 **OpenClaw Manager** 进入管理界面。

> 当前仓库暂未发布 GitHub Release。安装包是本机生成的交付物，位于 `packaging/output/`，该目录被 `.gitignore` 排除，不会提交到 Git 历史。

### 首次安装流程

1. 打开 **安装与配置**。
2. 勾选是否自动安装 Node.js 和 Gateway。
3. 如需配置模型，选择服务商并填写 API Key；API Key 不会显示在日志中。
4. 点击 **安装 OpenClaw**，等待进度完成。
5. 在 **概览** 或 **Gateway** 页面确认服务状态。

## 系统要求

- Windows 10 或 Windows 11，x64
- 在线安装需要网络访问 Node.js、npm 和 OpenClaw 官方包源
- 安装 Node.js 时需要通过 UAC 管理员授权
- 至少 2 GB 可用磁盘空间
- 终端用户不需要预装 .NET；发布包是 self-contained x64

## 开发环境

- .NET 8 SDK
- Windows desktop workload / WPF 支持
- Inno Setup 6（仅在需要生成安装包时使用）
- Git；GitHub 推送可使用 GitHub CLI `gh`

如果系统没有全局 `dotnet`，请先安装 .NET 8 SDK，或在 PowerShell 中将 SDK 路径设置为本机实际路径：

```powershell
$env:DOTNET_ROOT = "C:\path\to\dotnet"
$env:DOTNET_CLI_HOME = "$env:USERPROFILE\.dotnet-home"
$dotnet = "$env:DOTNET_ROOT\dotnet.exe"
```

### 构建和测试

```powershell
& $dotnet restore OpenClawManager.sln
& $dotnet build OpenClawManager.sln -c Release --no-restore
& $dotnet test OpenClawManager.sln -c Release --no-restore
& $dotnet format OpenClawManager.sln --verify-no-changes --no-restore
```

### 发布和打包

```powershell
& $dotnet publish src\OpenClawManager\OpenClawManager.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o packaging\publish

iscc packaging\OpenClawManager.iss
```

输出文件：

```text
packaging/output/OpenClawManagerSetup.exe
```

## 项目结构

```text
OpenClawManager.sln
├─ src/OpenClawManager/
│  ├─ App.xaml                 # WPF 应用入口和全局样式
│  ├─ MainWindow.xaml          # Fluent 风格主界面
│  ├─ MainWindow.xaml.cs       # 页面导航和 UI 操作编排
│  ├─ Core/Models/             # 状态、进度、配置和结果模型
│  ├─ Core/Services/           # 安装、配置、Gateway、日志、卸载服务
│  └─ Infrastructure/          # 进程执行、路径、提权和版本策略
├─ tests/OpenClawManager.Tests/ # xUnit 单元测试
├─ packaging/                  # Inno Setup 脚本和本地构建输出
├─ docs/                       # 设计文档和 Windows 冒烟测试
└─ .github/workflows/ci.yml    # GitHub Actions 构建与测试
```

## 数据和安全边界

管理器自身的数据存放在：

```text
%LOCALAPPDATA%\OpenClawManager
├─ state.json                  # 安装归属和当前状态
├─ logs\                      # JSON Lines 日志
└─ backups\                   # 配置备份和 manifest.json
```

OpenClaw 用户配置默认保留在：

```text
%USERPROFILE%\.openclaw
```

卸载时，管理器只删除 `state.json` 中记录为自有且用户明确勾选的资源。未知快捷方式、未知防火墙规则、用户环境变量和未确认的配置不会被批量清理。

## 测试文档

完整的 Windows 10/11 x64 清洁环境测试清单位于：

[`docs/testing/clean-windows-smoke-test.md`](docs/testing/clean-windows-smoke-test.md)

覆盖首次安装、Node.js 版本兼容性、下载失败、端口冲突、Gateway 重启、备份恢复和不同卸载选项。

## 旧脚本说明

仓库根目录中的以下文件保留为历史参考，不属于新桌面应用的运行时流程：

```text
setup.bat
install.ps1
build-installer.ps1
uninstall.bat
快捷操作.bat
使用指南.txt
plugins/edge-tts/
```

新应用通过 C# 服务直接调用 `node`、`npm`、`openclaw` 和 `msiexec.exe`，不调用这些旧脚本。

## 贡献

欢迎提交 Issue 或 Pull Request。提交代码前请至少运行：

```powershell
& $dotnet format OpenClawManager.sln --verify-no-changes --no-restore
& $dotnet test OpenClawManager.sln -c Release --no-restore
```

涉及安装、卸载、配置或密钥处理的改动，应同时补充测试和 Windows 冒烟测试说明。

## 许可证

本项目当前尚未在仓库中指定开源许可证。除非后续添加 `LICENSE` 文件或另有明确授权，代码版权仍归项目作者所有。
