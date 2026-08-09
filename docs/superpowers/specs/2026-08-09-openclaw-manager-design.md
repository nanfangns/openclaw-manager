# OpenClaw Manager 设计文档

- 日期：2026-08-09
- 状态：已确认，待实现
- 目标平台：Windows 10/11 x64
- 首版安装方式：联网安装

## 1. 产品定位

OpenClaw Manager 是一个 Windows 桌面安装器和管理器。它不重新实现聊天客户端，也不替代 OpenClaw Gateway；它负责把 OpenClaw 安装、配置、启动和维护流程包装成普通用户可以使用的桌面软件。

用户不需要接触 BAT、PowerShell、CMD 或 npm 命令。

核心闭环：

```text
检测环境 -> 安装 Node.js -> 安装 OpenClaw -> 配置模型
    -> 安装并启动 Gateway -> 健康检查 -> 日常管理/修复/卸载
```

## 2. 首版范围

### 包含

- GUI 安装向导
- Windows、CPU 架构、网络和磁盘检测
- Node.js 兼容性检测和安装
- OpenClaw 安装、版本检测和升级入口
- 模型提供商配置
- API Key 隐藏输入和脱敏日志
- Gateway 安装、启动、停止、重启和状态检查
- 打开 OpenClaw Control UI
- 安装日志、Gateway 日志和诊断信息
- 安装失败重试
- 配置备份和恢复
- 安全卸载
- 桌面快捷方式、开始菜单入口和应用卸载注册

### 不包含

- 聊天界面
- 插件市场
- 微信、Telegram、Discord 等频道管理
- Edge TTS 自动部署
- 默认开放局域网访问
- 自动删除用户原有配置

插件和频道管理作为后续独立功能，不进入第一版安装主流程。

## 3. 技术选型

- 语言：C#
- 框架：.NET 8
- UI：WPF
- UI 架构：MVVM
- 发布：self-contained Windows x64
- 安装包：Inno Setup 或 WiX
- 图标：Fluent 风格图标；OpenClaw 标识仅在许可确认后使用

选择 WPF 的原因：Windows 原生能力成熟，适合处理 UAC、快捷方式、进程、服务、卸载注册和本地文件；不需要引入浏览器运行时，也不把安装逻辑绑定到 BAT 或前端开发工具链。

最终产品是持久化安装的 `OpenClawManager.exe`。安装过程可以使用打包工具，但运行时不以脚本作为用户界面。

## 4. 软件架构

```text
WPF UI / MVVM
    |
    +-- InstallCoordinator       安装状态机和步骤编排
    +-- EnvironmentService       Windows、Node、npm、网络、磁盘检测
    +-- OpenClawCliService        OpenClaw 命令封装
    +-- GatewayService            Gateway 生命周期管理
    +-- ConfigService             配置备份、恢复和校验
    +-- CredentialService         密钥输入、引用和脱敏
    +-- ProcessRunner             进程启动、退出码、超时、取消、日志
    +-- StateStore                本软件状态和安装所有权记录
    +-- LogService                文件日志和界面日志流
```

### 4.1 ProcessRunner

所有外部命令都通过 `System.Diagnostics.Process` 启动：

- 使用独立参数列表，不拼接 `cmd /c` 字符串
- 捕获 stdout 和 stderr
- 返回退出码、耗时和取消状态
- 支持超时
- 支持用户取消
- 对 API Key、Token 和密钥参数做日志脱敏
- 命令失败时由调用方决定重试或回滚

禁止使用“进程结束即成功”的判断。

### 4.2 InstallCoordinator

安装过程使用显式状态机：

```text
Detecting
  -> InstallingNode
  -> VerifyingNode
  -> InstallingOpenClaw
  -> VerifyingOpenClaw
  -> BackingUpConfig
  -> ConfiguringModel
  -> InstallingGateway
  -> StartingGateway
  -> HealthChecking
  -> Completed
```

每个步骤必须定义：

- 开始条件
- 执行动作
- 成功条件
- 失败原因
- 可否重试
- 可否回滚
- 用户可见提示

安装状态持久化到本地，软件关闭后可以恢复到最近一个安全检查点。

## 5. 用户界面

视觉方向采用 A：Fluent 后台型布局。

### 5.1 导航

左侧导航固定显示：

1. 概览
2. 安装 / 修复
3. Gateway
4. 模型配置
5. 日志
6. 卸载

### 5.2 概览页

显示以下状态卡片：

- OpenClaw 版本
- Node.js 版本和兼容性
- Gateway 运行状态
- 当前端口
- 当前模型提供商和模型
- 运行时长

主要操作：

- 打开控制面板
- 启动 Gateway
- 重启 Gateway
- 修复环境
- 查看日志

### 5.3 首次安装页

使用带步骤状态的安装向导：

1. 系统检测
2. Node.js
3. OpenClaw
4. 模型配置
5. Gateway
6. 健康检查

每一步都显示当前动作、进度、耗时、输出摘要和错误详情入口。

### 5.4 视觉资源

- 使用 Fluent 风格的状态、设置、日志、服务、刷新、修复和删除图标
- 颜色语义固定：绿色成功、黄色警告、红色错误、蓝色主操作
- 使用自制龙虾抽象图标作为默认品牌图形
- 若使用 OpenClaw 官方标识，随软件附带许可和来源说明

## 6. 安装流程

### 6.1 环境检测

检测：

- Windows 10/11
- x64 架构
- 管理员权限是否可用
- 网络是否可用
- 系统盘和目标盘空间
- 现有 Node.js 和 npm
- 现有 OpenClaw
- Gateway 端口是否占用

检测失败时给出具体修复建议，不直接退出并要求用户猜原因。

### 6.2 Node.js

- 如果已有 Node.js 满足 OpenClaw 当前最低版本，则复用
- 如果版本不满足，则提供安装兼容 LTS 的选项
- 安装后刷新进程环境并重新验证 `node -v`、`npm -v`
- 记录 Node.js 是否由本软件安装
- 卸载时只有在用户明确确认且记录表明由本软件安装时才删除 Node.js

版本策略不能只检查主版本，也不能永久写死旧下载地址；应由可更新的运行时策略提供最低版本和下载信息，并在下载后进行完整性校验。

### 6.3 OpenClaw

- 默认安装稳定版本 `openclaw@latest`
- 验证 `openclaw --version`
- 已存在兼容版本时默认复用
- 提供升级和重新安装入口
- 记录实际安装版本和命令输出

### 6.4 模型配置

界面提供常用提供商和自定义 OpenAI 兼容接口。

配置原则：

- API Key 输入框默认隐藏
- 不把 API Key 写入普通日志
- 不把 API Key 拼接进可复制的命令文本
- 优先调用 OpenClaw 官方 onboarding/config 命令
- 配置完成后执行配置校验
- 可选执行一次轻量健康验证

模型名称优先从 OpenClaw 的模型列表或配置结果读取，避免维护一份长期过时的模型硬编码表。

### 6.5 Gateway

- 使用官方 Gateway 安装命令
- 启动后检查进程、端口和 Gateway 健康状态
- 提供开机启动状态
- 默认不创建局域网开放规则
- 若以后增加局域网访问，必须使用明确的用户开关

## 7. 配置、备份和安全

### 7.1 软件数据

```text
C:\Program Files\OpenClaw Manager\
    OpenClawManager.exe
    assets\

%LOCALAPPDATA%\OpenClawManager\
    logs\
    backups\
    state.json
    runtime.json
```

### 7.2 OpenClaw 配置

配置操作前备份 `%USERPROFILE%\.openclaw\` 到软件备份目录。

恢复支持：

- 最近备份
- 指定备份
- 保留当前配置并导入可选部分

### 7.3 安装所有权

`state.json` 记录本软件创建的资源：

- Node.js 安装记录
- OpenClaw 安装版本
- Gateway 安装记录
- 快捷方式路径
- 防火墙规则名称（如果未来用户启用）
- 配置备份记录

卸载只处理这些记录中的资源，不盲删用户的环境变量或目录。

## 8. 日志和错误处理

日志位置：

```text
%LOCALAPPDATA%\OpenClawManager\logs\YYYY-MM-DD.log
```

日志包含：

- 时间
- 步骤
- 命令名称
- 参数脱敏摘要
- stdout/stderr
- 退出码
- 开始和结束时间
- 错误分类

界面提供普通日志和详细日志两种视图。命令失败时不显示“安装完成”，而是显示失败步骤、原始错误摘要、重试按钮和打开日志按钮。

## 9. 卸载流程

卸载向导明确询问：

- 是否保留 OpenClaw 配置
- 是否删除工作区和会话数据
- 是否删除本软件安装的 Node.js
- 是否删除 Gateway 服务
- 是否删除桌面和开始菜单快捷方式

卸载顺序：

```text
停止 Gateway -> 卸载 Gateway -> 卸载 OpenClaw
    -> 按选择处理配置 -> 清理本软件资源 -> 删除应用本体
```

卸载完成后保留可选的备份路径提示。

## 10. 最终交付

交付文件：

```text
OpenClawManagerSetup.exe
```

安装结果：

- 开始菜单入口
- 桌面快捷方式（可选）
- Windows 应用卸载项
- 持久化管理器目录
- 不指向临时目录的快捷方式
- 应用图标和版本信息

## 11. 测试策略

### 单元测试

- 版本比较
- 端口检测
- 状态机迁移
- 日志脱敏
- 配置备份路径
- 安装所有权记录
- 退出码映射

### 集成测试

- Node.js 已存在且兼容
- Node.js 不存在
- Node.js 版本过低
- npm 安装失败
- OpenClaw 已存在
- OpenClaw 安装失败
- Gateway 端口占用
- Gateway 启动失败
- 配置校验失败
- 用户取消安装
- 安装中断后恢复
- 保留配置卸载
- 完整卸载

### 干净系统验收

至少在一台干净的 Windows 10/11 x64 环境验证：

1. 双击安装包可以完成安装
2. 安装失败不会伪报成功
3. 重启后 Gateway 状态正确
4. 桌面快捷方式可用
5. 管理器可以重新打开并读取状态
6. 卸载不会留下失效快捷方式或无法删除的服务

## 12. 迁移现有目录

现有文件不继续作为产品入口：

- `setup.bat`
- `install.ps1`
- `build-installer.ps1`
- `快捷操作.bat`
- `uninstall.bat`

在新软件完成基础功能前保留这些文件作为参考和回滚材料；新程序不调用它们作为核心安装流程。Edge TTS 目录暂不进入第一版主安装流程，后续单独设计插件扩展机制。

## 13. 成功标准

首版完成的判断标准：

- 用户只需运行一个 GUI 安装包
- 全程不需要手动打开终端
- 安装成功后可以从管理器查看 Gateway 状态
- 能打开 OpenClaw Control UI
- Gateway 可以启动、停止和重启
- 失败时能看到真实错误并重试
- 卸载行为可预测且不误删用户资源
- 快捷方式和卸载项在安装后仍然有效
