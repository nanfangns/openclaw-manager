# OpenClaw Manager

一个面向 Windows 10/11 x64 的 OpenClaw 图形化安装器与本机管理工具。

[![CI](https://github.com/nanfangns/openclaw-manager/actions/workflows/ci.yml/badge.svg)](https://github.com/nanfangns/openclaw-manager/actions/workflows/ci.yml)
[![Latest Release](https://img.shields.io/github/v/release/nanfangns/openclaw-manager?label=latest%20release)](https://github.com/nanfangns/openclaw-manager/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/nanfangns/openclaw-manager/total?label=downloads)](https://github.com/nanfangns/openclaw-manager/releases)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-0078D4)](https://www.microsoft.com/windows/)

<p align="center">
  <img src="src/OpenClawManager/Assets/OpenClawManagerLogo.png" alt="OpenClaw Manager logo" width="180">
</p>

## 项目简介

OpenClaw Manager 的目标是把 OpenClaw 的安装、运行环境准备和日常维护集中到一个轻量的 Windows 桌面软件中。它适合不想手动执行多段 BAT、PowerShell 和命令行的人，也适合需要明确控制 Gateway、配置备份和卸载范围的本机用户。

本项目是 **OpenClaw 的安装与管理工具**，不是 OpenClaw 本体，也不是聊天客户端。OpenClaw 项目本身请前往[官方仓库](https://github.com/openclaw/openclaw)了解。

> 当前版本：`v0.1.3`。项目处于早期预览阶段，核心安装和管理流程已落地，界面和兼容性仍会持续完善。

## 为什么需要它

手动安装 OpenClaw 往往需要依次处理 Node.js、npm、CLI、Gateway、模型配置、端口和本地配置文件。OpenClaw Manager 将这些步骤拆成可见、可取消、可追踪的操作，并记录安装归属，避免卸载时误删用户已有环境。

核心原则：

- **先检测，再修改**：尽量复用已有的兼容 Node.js，不重复安装。
- **操作可追踪**：记录管理器安装的资源、操作结果和脱敏日志。
- **配置先备份**：修改 `.openclaw` 前创建带 SHA-256 清单的备份。
- **卸载有边界**：只处理管理器记录为自有、且用户明确勾选的资源。
- **默认收敛**：不默认开放 LAN 访问，不默认修改用户环境变量，不默认删除已有配置。

## 功能

| 模块 | 能力 |
| --- | --- |
| 环境检测 | 检查 Windows 架构、Node.js、npm、OpenClaw CLI、Gateway 端口和网络/磁盘条件 |
| 一键安装 | 必要时下载并校验官方 Node.js x64 MSI，然后安装并验证 `openclaw@latest` |
| Gateway | 安装、启动、停止、重启、状态检查和健康检查；默认端口 `18789` |
| 模型配置 | 支持 OpenAI、Anthropic、Google Gemini、DeepSeek、OpenRouter 和自定义 OpenAI-compatible 服务 |
| 配置备份 | 备份 `%USERPROFILE%\.openclaw`，保存时间戳、文件清单和 SHA-256 哈希 |
| 日志 | 记录安装和服务操作；API Key、Token、Secret、Password 等敏感值自动脱敏 |
| 安全卸载 | 分别选择 Gateway、OpenClaw CLI、Node.js、配置、工作区和管理器数据是否清理 |

## 快速开始

### 方式一：下载发布版

前往 [GitHub Releases](https://github.com/nanfangns/openclaw-manager/releases/latest) 下载最新的 `OpenClawManagerSetup.exe`，然后按安装向导操作。

当前版本：

- [下载 OpenClaw Manager v0.1.3](https://github.com/nanfangns/openclaw-manager/releases/tag/v0.1.3)
- [直接下载安装包](https://github.com/nanfangns/openclaw-manager/releases/download/v0.1.3/OpenClawManagerSetup.exe)

安装程序会：

1. 安装到 `Program Files\OpenClaw Manager`。
2. 创建开始菜单快捷方式。
3. 可选创建桌面快捷方式。
4. 可选在安装结束后启动管理器。

### 方式二：运行本地安装包

如果你已经在本地构建过安装包，运行：

```text
packaging/output/OpenClawManagerSetup.exe
```

### 首次使用

1. 打开 **安装与配置** 页面。
2. 让软件检测当前 Node.js 和 OpenClaw 环境。
3. 如果 Node.js 版本不兼容，保持勾选 **自动安装 Node.js**。
4. 根据需要勾选 **安装并启动 Gateway 服务**。
5. 如果要配置模型，选择服务商并填写模型 ID、API Key；自定义服务商还需要填写 Base URL。
6. 安装完成后，在 **概览** 或 **Gateway** 页面检查运行状态。

Node.js MSI 安装需要管理员权限。网络中断、UAC 取消、端口被占用或官方包源不可访问时，软件会在界面和日志中报告失败原因。

## 安装包校验

下载后可以在 PowerShell 中校验文件哈希：

```powershell
Get-FileHash .\OpenClawManagerSetup.exe -Algorithm SHA256
```

`v0.1.3` 安装包 SHA-256：

```text
44efe3409189687d82f8bfa0c87c90953df8d3d8e6c7aa388dcd5198ff4915e8
```

## 系统要求

- Windows 10 或 Windows 11，x64
- 在线安装需要访问 Node.js、npm 和 OpenClaw 官方包源
- 安装 Node.js 时需要管理员权限和 UAC 授权
- 至少 2 GB 可用磁盘空间，实际占用取决于 Node.js、OpenClaw 和日志/备份数量
- 终端用户不需要预装 .NET；GitHub Release 提供 self-contained x64 安装包

## 数据位置与安全边界

管理器自身的数据默认位于：

```text
%LOCALAPPDATA%\OpenClawManager
├─ state.json          # 安装归属、当前状态和资源记录
├─ logs\               # JSON Lines 操作日志
└─ backups\            # 配置备份及 manifest.json
```

OpenClaw 用户配置默认位于：

```text
%USERPROFILE%\.openclaw
```

卸载行为遵循以下规则：

| 资源 | 默认行为 | 说明 |
| --- | --- | --- |
| 已有 Node.js | 保留 | 只有管理器安装并且用户勾选时才尝试卸载 |
| 已有 OpenClaw | 保留 | 只处理管理器记录为自有的 CLI |
| Gateway | 保留 | 可在卸载页单独选择停止和卸载 |
| `.openclaw` 配置 | 保留 | 删除前会先备份，并要求用户明确选择 |
| `.openclaw/workspace` | 保留 | 不随普通卸载自动删除 |
| 管理器日志和备份 | 保留 | 可在卸载页单独选择清理 |

API Key 不会直接显示在运行日志中。需要注意：配置文件和备份可能包含用户主动保存的凭据，使用备份、迁移或提交日志前请自行检查敏感内容。

## 当前范围

当前版本专注于安装和本机管理，暂不包含：

- 聊天界面
- 插件市场
- Channel / 消息渠道管理
- Edge TTS 自动部署
- 默认 LAN 访问

仓库中的 `setup.bat`、`install.ps1`、`build-installer.ps1`、`uninstall.bat`、`快捷操作.bat` 和 `使用指南.txt` 是历史脚本或参考资料，不属于新桌面应用的运行时界面。新应用通过 C# 服务直接调用 `node`、`npm`、`openclaw` 和 `msiexec.exe`。

## 开发环境

### 依赖

- .NET 8 SDK
- Windows Desktop / WPF 支持
- Inno Setup 6（仅生成安装包时需要）
- Git
- 可选：GitHub CLI `gh`，用于发布 Release

如果系统没有全局 `dotnet`，可以先安装 .NET 8 SDK，或在当前 PowerShell 会话中指定 SDK：

```powershell
$env:DOTNET_ROOT = "C:\path\to\dotnet"
$env:DOTNET_CLI_HOME = "$env:USERPROFILE\.dotnet-home"
$dotnet = "$env:DOTNET_ROOT\dotnet.exe"
```

### 克隆项目

```powershell
git clone https://github.com/nanfangns/openclaw-manager.git
cd openclaw-manager
```

### 构建和测试

```powershell
& $dotnet restore OpenClawManager.sln
& $dotnet build OpenClawManager.sln -c Release --no-restore
& $dotnet test OpenClawManager.sln -c Release --no-restore
& $dotnet format OpenClawManager.sln --verify-no-changes --no-restore
```

### 生成 self-contained 安装包

```powershell
& $dotnet publish src\OpenClawManager\OpenClawManager.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o packaging\publish

iscc packaging\OpenClawManager.iss
```

安装包输出到：

```text
packaging/output/OpenClawManagerSetup.exe
```

本地构建产物和 `.dotnet-home` 不应提交到 Git；仓库已经通过 `.gitignore` 排除相关目录。

## 项目结构

```text
OpenClawManager.sln
├─ src/OpenClawManager/
│  ├─ App.xaml / App.xaml.cs       # WPF 入口和全局资源
│  ├─ MainWindow.xaml(.cs)         # 主界面、导航和操作编排
│  ├─ Core/Models/                 # 状态、进度、配置和结果模型
│  ├─ Core/Services/               # 安装、配置、Gateway、日志和卸载服务
│  ├─ Infrastructure/              # 进程、路径、提权和版本策略
│  └─ Assets/                      # 应用 Logo 和 Windows ICO
├─ tests/OpenClawManager.Tests/    # xUnit 单元测试
├─ packaging/                     # Inno Setup 脚本和本地打包输出
├─ docs/testing/                  # Windows 清洁环境冒烟测试
├─ docs/superpowers/               # 设计和实施文档
└─ .github/workflows/ci.yml        # GitHub Actions 构建与测试
```

## 测试与验证

完整的 Windows 10/11 x64 清洁环境测试清单位于 [`docs/testing/clean-windows-smoke-test.md`](docs/testing/clean-windows-smoke-test.md)，覆盖：

- 首次安装和已有环境复用
- Node.js 版本兼容性
- 下载失败、UAC 取消和端口冲突
- Gateway 启动、停止、重启和健康检查
- 配置备份与恢复
- 不同卸载选项下的资源归属和保留行为

提交涉及安装、卸载、配置或密钥处理的改动时，请同时补充单元测试和 Windows 冒烟测试说明。

## 贡献

欢迎通过 [Issue](https://github.com/nanfangns/openclaw-manager/issues) 报告问题，或提交 Pull Request。

提交前至少运行：

```powershell
& $dotnet format OpenClawManager.sln --verify-no-changes --no-restore
& $dotnet test OpenClawManager.sln -c Release --no-restore
```

Issue 建议包含：

- Windows 版本和系统架构
- OpenClaw Manager 版本
- Node.js、npm 和 OpenClaw 版本
- 可复现步骤
- 脱敏后的日志或错误信息

请不要在 Issue、日志或截图中粘贴 API Key、Token、Cookie、密码或完整配置文件。

## 许可证

本项目采用 [MIT License](LICENSE)。你可以自由使用、复制、修改、合并、发布、分发、再许可和销售本项目，但需要在副本中保留版权声明和许可证文本。

本许可证适用于项目作者维护的源代码和文档；第三方依赖、素材或仓库中另有说明的内容仍以其各自的许可证为准。

## 相关链接

- [OpenClaw 官方仓库](https://github.com/openclaw/openclaw)
- [OpenClaw Manager Releases](https://github.com/nanfangns/openclaw-manager/releases)
- [Windows 清洁环境测试清单](docs/testing/clean-windows-smoke-test.md)
