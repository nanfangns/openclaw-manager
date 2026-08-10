# OpenClaw Manager 架构说明

本文档描述当前代码实现，不记录已经废弃的 BAT、PowerShell 或插件方案。

## 产品边界

OpenClaw Manager 是 Windows 10/11 x64 桌面安装器和本机管理工具，负责：

- 检测并准备 Node.js、npm 和 OpenClaw CLI 环境
- 安装和验证 OpenClaw
- 安装、启动、停止、重启和检查 Gateway
- 配置模型服务商和凭据
- 备份、恢复和校验 `.openclaw` 配置
- 记录脱敏日志
- 按资源归属执行安全卸载

它不是聊天客户端，也不重新实现 OpenClaw Gateway。当前运行时不依赖 BAT、PowerShell 或命令提示符界面。

## 目录职责

```text
src/OpenClawManager/
├─ App.xaml(.cs)                 # WPF 入口、依赖组装和全局样式
├─ MainWindow.xaml(.cs)          # 页面布局、导航和用户操作编排
├─ Core/Models/                  # 不可变状态、配置、进度和结果模型
├─ Core/Services/                # 安装、验证、诊断和外部命令适配器
└─ Infrastructure/               # 路径、进程、提权和版本策略

tests/OpenClawManager.Tests/     # 服务层和策略层单元测试
packaging/OpenClawManager.iss    # Inno Setup 安装包定义
docs/                            # 架构和 Windows 测试文档
```

## 运行时数据流

```text
WPF MainWindow
    │
    ├─ EnvironmentService ── 检测 Windows、Node.js、npm、OpenClaw 和端口
    ├─ InstallCoordinator ── 编排 Node.js、OpenClaw、Gateway 和模型配置
    ├─ InstallationVerifier ── 安装后和手动触发的完整验证
    ├─ DiagnosticsService ─── 收集脱敏状态并导出诊断包
    ├─ GatewayService ────── 执行 Gateway 生命周期操作
    ├─ ConfigService ─────── 备份、恢复和校验 .openclaw
    ├─ UninstallService ──── 按归属和用户勾选清理资源
    └─ LogService ────────── 写入脱敏 JSON Lines 日志
             │
             └─ ProcessRunner ── 安全传递参数并检查退出码
                    │
                    └─ node / npm / openclaw / msiexec.exe
```

## 关键设计

### 外部命令

所有外部进程都通过 `ProcessRunner` 执行。命令参数使用参数集合传递，不拼接到未经处理的 shell 字符串中；服务层检查退出码、标准输出、标准错误和超时结果。

安装 Node.js 或 npm 全局包后，`PathEnvironment` 会合并用户级、机器级和当前进程的 PATH，再重新检测 `node`、`npm` 和 `openclaw`，避免当前 GUI 进程继续使用安装前的旧 PATH。

### 安装后验证和诊断

`InstallationVerifier` 按环境、Node.js、npm、OpenClaw CLI、配置、Gateway 连接和模型服务逐项检查。Gateway 验证不只依赖启动命令退出码，还解析 `openclaw gateway status --json` 中的运行状态和连接探测结果；模型验证使用 `openclaw models status --json --probe`，仅在用户明确配置模型或手动运行诊断时执行。

`DiagnosticsService` 不读取或打包 `.openclaw` 原始配置内容，只输出脱敏后的路径、版本、状态、检查摘要和最近日志。导出包为 `%LOCALAPPDATA%\OpenClawManager\diagnostics` 下的 ZIP 文件。

### 安装归属

`state.json` 记录由管理器安装的 Node.js、OpenClaw CLI、Gateway 和快捷方式等资源。卸载服务只处理有记录且用户明确勾选的资源，不批量删除未知文件、环境变量或配置。

### 配置和凭据

修改 `%USERPROFILE%\.openclaw` 前创建带文件清单和 SHA-256 哈希的备份。API Key 通过进程环境传递给配置命令，日志写入前经过脱敏处理。

### 本地默认值

- Gateway 默认使用本机端口 `18789`。
- 不默认开放局域网访问。
- 不默认修改用户级环境变量。
- 不默认删除已有 OpenClaw 配置和工作区。

## 构建产物

开发构建输出由 .NET SDK 管理，发布构建使用 self-contained `win-x64` 配置：

```text
packaging/publish/                 # 临时发布目录，不提交
packaging/output/                  # Inno Setup 安装包，不提交
```

安装包脚本只负责将发布目录打包为 Windows 安装程序，应用本身的安装和管理逻辑位于 `src/OpenClawManager/`。

## 验证策略

- `dotnet test`：验证命令执行、版本策略、安装编排、配置备份恢复、状态存储和卸载归属。
- 新增测试覆盖 PATH 合并、Gateway 嵌套 JSON/连接探测、模型探测失败识别、安装后验证和诊断包脱敏。
- `dotnet format --verify-no-changes`：检查代码格式。
- Windows 清洁环境测试：验证真实安装包、UAC、网络失败、端口冲突、Gateway 和卸载流程，详见 [`testing/clean-windows-smoke-test.md`](testing/clean-windows-smoke-test.md)。
