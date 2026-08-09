# ============================================================
#  OpenClaw 一键安装 by 小许
#  PowerShell 主安装脚本
# ============================================================
param([switch]$SkipAdmin)

$ErrorActionPreference = "Continue"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# ─── 颜色工具 ───────────────────────────────────────────────
function Write-Step   { param($n,$t) Write-Host "`n[$n] " -NoNewline -ForegroundColor Cyan; Write-Host $t -ForegroundColor White }
function Write-OK     { param($t) Write-Host "   ✅ $t" -ForegroundColor Green }
function Write-Warn   { param($t) Write-Host "   ⚠️  $t" -ForegroundColor Yellow }
function Write-Err    { param($t) Write-Host "   ❌ $t" -ForegroundColor Red }
function Write-Info   { param($t) Write-Host "   ℹ️  $t" -ForegroundColor Gray }
function Write-Head   { param($t) Write-Host "`n$t" -ForegroundColor Magenta }
function Write-Div    { Write-Host ("─" * 55) -ForegroundColor DarkGray }

# ─── 超时执行工具 ───────────────────────────────────────────
# 注意：Start-Job 启动的是独立进程，不会继承主进程的 $env:PATH 修改！
# 所以必须把当前 PATH 传进去，否则刚装的 Node.js/npm 找不到。
function Invoke-WithTimeout {
    param(
        [scriptblock]$ScriptBlock,
        [int]$TimeoutSec = 120,
        [string]$Label = "command"
    )
    $currentPath = $env:PATH
    $job = Start-Job -ScriptBlock {
        param($block, $path)
        $env:PATH = $path
        try { & $block } catch { throw }
    } -ArgumentList $ScriptBlock, $currentPath
    $completed = $job | Wait-Job -Timeout $TimeoutSec
    if ($null -eq $completed) {
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        Write-Warn "$Label 超时（${TimeoutSec}s），跳过"
        return $false
    }
    $result = Receive-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    return $true
}

# ─── 直接执行带超时（主进程 PATH 正确传递）────────────────
# 和 Invoke-WithTimeout 一样都会传 PATH，区别在于不跳过错误
function Invoke-Direct {
    param(
        [scriptblock]$ScriptBlock,
        [int]$TimeoutSec = 180,
        [string]$Label = "command"
    )
    $currentPath = $env:PATH
    $job = Start-Job -ScriptBlock {
        param($block, $path)
        $env:PATH = $path
        & $block
    } -ArgumentList $ScriptBlock, $currentPath
    $completed = $job | Wait-Job -Timeout $TimeoutSec
    if ($null -eq $completed) {
        Stop-Job $job -ErrorAction SilentlyContinue
        Remove-Job $job -Force -ErrorAction SilentlyContinue
        Write-Warn "$Label 超时（${TimeoutSec}s）"
        return $false
    }
    $result = Receive-Job $job -ErrorAction SilentlyContinue
    Remove-Job $job -Force -ErrorAction SilentlyContinue
    return $true
}

# ─── 环境预检工具 ───────────────────────────────────────────
function Test-Network {
    try {
        $r = Invoke-WebRequest -Uri "https://www.baidu.com" -UseBasicParsing -TimeoutSec 5 -Method Head
        return $true
    } catch {
        return $false
    }
}
function Test-PortFree {
    param([int]$Port = 18789)
    $conn = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    return ($null -eq $conn -or $conn.Count -eq 0)
}

# ─── Banner ─────────────────────────────────────────────────
Clear-Host
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════════╗" -ForegroundColor DarkCyan
Write-Host "  ║                                                      ║" -ForegroundColor DarkCyan
Write-Host "  ║        🐾  OpenClaw 一键安装 by 小许  🐾           ║" -ForegroundColor DarkCyan
Write-Host "  ║                                                      ║" -ForegroundColor DarkCyan
Write-Host "  ║   自动安装 Node.js + OpenClaw + 模型 + 插件         ║" -ForegroundColor DarkCyan
Write-Host "  ║   全程交互配置，装完即用                             ║" -ForegroundColor DarkCyan
Write-Host "  ║                                                      ║" -ForegroundColor DarkCyan
Write-Host "  ╚══════════════════════════════════════════════════════╝" -ForegroundColor DarkCyan
Write-Host ""

# ============================================================
#  STEP 1: 检查管理员权限
# ============================================================
Write-Step "1/8" "检查系统环境"
Write-Div

if (-not $SkipAdmin) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Warn "需要管理员权限，正在提权..."
        Start-Process powershell.exe "-ExecutionPolicy Bypass -File `"$PSCommandPath`" -SkipAdmin" -Verb RunAs
        exit
    }
}
Write-OK "管理员权限"

$osInfo = Get-CimInstance Win32_OperatingSystem
Write-OK "系统: Windows $($osInfo.Version)"

# 检查 Windows 版本（winget 需要 1809+）
$buildNum = [int]($osInfo.BuildNumber)
if ($buildNum -lt 17763) {
    Write-Warn "Windows 版本较旧（Build $buildNum），winget 可能不可用"
    Write-Info "脚本会自动 fallback 到直接下载安装"
} else {
    Write-OK "Windows 版本支持 winget (Build $buildNum)"
}

# ─── 环境预检 ───────────────────────────────────────────────
Write-Host ""
Write-Info "环境预检..."

# 检查网络
if (Test-Network) {
    Write-OK "网络连接正常"
} else {
    Write-Err "未检测到网络连接！"
    Write-Info "请先连接 WiFi 或网线，然后重新运行脚本"
    Write-Info "如果是公司网络需要代理，请先配置系统代理或设置环境变量："
    Write-Info "  `$env:HTTP_PROXY = `"http://代理地址:端口`""
    Write-Info "  `$env:HTTPS_PROXY = `"http://代理地址:端口`""
    Read-Host "按回车退出"
    exit 1
}

# 检查磁盘空间（至少需要 2GB）
$disk = Get-CimInstance Win32_LogicalDisk -Filter "DeviceID='C:'"
$freeGB = [math]::Round($disk.FreeSpace / 1GB, 1)
if ($freeGB -ge 2) {
    Write-OK "C盘剩余空间: ${freeGB}GB"
} else {
    Write-Err "C盘空间不足！剩余 ${freeGB}GB，至少需要 2GB"
    Read-Host "按回车退出"
    exit 1
}

# 检查端口
if (Test-PortFree -Port 18789) {
    Write-OK "端口 18789 可用"
} else {
    Write-Warn "端口 18789 已被占用！Gateway 可能无法启动"
    Write-Info "占用进程:"
    Get-NetTCPConnection -LocalPort 18789 -ErrorAction SilentlyContinue |
        Select-Object OwningProcess -Unique |
        ForEach-Object { Get-Process -Id $_.OwningProcess -ErrorAction SilentlyContinue } |
        ForEach-Object { Write-Info "  → $($_.ProcessName) (PID: $($_.Id))" }
    Write-Host ""
    $portOk = Read-Host "  是否继续安装？(y/N)"
    if ($portOk -ne "y") { exit 1 }
}

# 检查中文用户名（可能影响路径）
$userProfile = $env:USERPROFILE
if ($userProfile -match '[^\x00-\x7F]') {
    Write-Warn "用户名包含非英文字符: $userProfile"
    Write-Info "部分工具可能路径异常，建议安装后观察是否报错"
} else {
    Write-OK "用户名路径正常: $userProfile"
}

# 检查已有 OpenClaw 安装
$existingOc = Get-Command openclaw -ErrorAction SilentlyContinue
if ($existingOc) {
    try {
        $existingVer = & openclaw --version 2>$null
        Write-Warn "检测到已安装 OpenClaw: $existingVer"
        Write-Info "将跳过安装，直接进入配置"
    } catch {}
}

# ============================================================
#  STEP 2: 安装 Node.js
# ============================================================
Write-Step "2/8" "检查 Node.js"
Write-Div

$nodeOk = $false
try {
    $nodeVer = & node -v 2>$null
    if ($nodeVer -match "v(\d+)") {
        $major = [int]$Matches[1]
        if ($major -ge 22) {
            Write-OK "Node.js 已安装: $nodeVer"
            $nodeOk = $true
        } else {
            Write-Warn "Node.js 版本过低 ($nodeVer)，需要 v22+"
        }
    }
} catch {}

if (-not $nodeOk) {
    Write-Host "   📥 正在安装 Node.js..." -ForegroundColor Yellow

    $nodeInstalled = $false

    # 尝试 winget（有超时保护）
    $hasWinget = Get-Command winget -ErrorAction SilentlyContinue
    if ($hasWinget -and -not $nodeInstalled) {
        Write-Info "使用 winget 安装..."
        $wingetOk = Invoke-WithTimeout -TimeoutSec 180 -Label "winget 安装 Node.js" -ScriptBlock {
            & winget install OpenJS.NodeJS --accept-package-agreements --accept-source-agreements 2>$null
        }
        # 验证是否装好了
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
        try { $null = & node -v 2>$null; $nodeInstalled = $true } catch {}
    }

    # winget 失败或超时，fallback 到 MSI 直接下载
    if (-not $nodeInstalled) {
        Write-Info "从 nodejs.org 下载安装..."
        $url = "https://nodejs.org/dist/v22.16.0/node-v22.16.0-x64.msi"
        $msi = "$env:TEMP\node-install.msi"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $dlOk = Invoke-WithTimeout -TimeoutSec 120 -Label "下载 Node.js" -ScriptBlock {
            Invoke-WebRequest -Uri $url -OutFile $msi -UseBasicParsing
        }
        if ($dlOk -and (Test-Path $msi)) {
            Start-Process msiexec.exe -ArgumentList "/i `"$msi`" /qn /norestart" -Wait -NoNewWindow
            Remove-Item $msi -Force -ErrorAction SilentlyContinue
            $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
        } else {
            Write-Err "Node.js 下载超时或失败，请检查网络后重试"
        }
    }

    # 刷新 PATH
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

    try {
        $nodeVer = & node -v 2>$null
        Write-OK "Node.js 安装成功: $nodeVer"
    } catch {
        Write-Err "Node.js 安装失败，请重启后重试"
        Read-Host "按回车退出"
        exit 1
    }
}

# ============================================================
#  STEP 3: 安装 OpenClaw
# ============================================================
Write-Step "3/8" "安装 OpenClaw"
Write-Div

$ocInstalled = $false
try {
    $ocVer = & openclaw --version 2>$null
    Write-OK "OpenClaw 已安装: $ocVer"
    $ocInstalled = $true
} catch {}

if (-not $ocInstalled) {
    Write-Host "   📥 正在安装 OpenClaw..." -ForegroundColor Yellow
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

    # 直接 npm install -g openclaw（不走 iwr | iex，国内容易超时）
    Write-Info "使用 npm install -g openclaw..."
    $npmOk = Invoke-Direct -TimeoutSec 180 -Label "npm install openclaw" -ScriptBlock {
        & npm install -g openclaw 2>&1
    }

    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")

    try {
        $ocVer = & openclaw --version 2>$null
        Write-OK "OpenClaw 安装成功: $ocVer"
    } catch {
        Write-Err "OpenClaw 安装失败"
        Read-Host "按回车退出"
        exit 1
    }
}

# ============================================================
#  STEP 4: 选择 AI 模型提供商  ⭐ 核心交互
# ============================================================
Write-Step "4/8" "配置 AI 模型"
Write-Div

Write-Host ""
Write-Host "  ┌─────────────────────────────────────────────────┐" -ForegroundColor White
Write-Host "  │  选择你要使用的 AI 模型提供商                    │" -ForegroundColor White
Write-Host "  ├─────────────────────────────────────────────────┤" -ForegroundColor White
Write-Host "  │                                                 │" -ForegroundColor White
Write-Host "  │  【官方渠道 - 只需 API Key】                    │" -ForegroundColor Yellow
Write-Host "  │  [1] Anthropic  (Claude 系列)    ⭐ 推荐        │" -ForegroundColor White
Write-Host "  │  [2] OpenAI     (GPT 系列)                      │" -ForegroundColor White
Write-Host "  │  [3] Google     (Gemini 系列)                   │" -ForegroundColor White
Write-Host "  │                                                 │" -ForegroundColor White
Write-Host "  │  【第三方渠道 - 需要填地址 + Key】              │" -ForegroundColor Yellow
Write-Host "  │  [4] DeepSeek   (国产性价比之王)                │" -ForegroundColor White
Write-Host "  │  [5] OpenRouter (聚合平台，啥都有)              │" -ForegroundColor White
Write-Host "  │  [6] Kimi       (月之暗面)                      │" -ForegroundColor White
Write-Host "  │  [7] 通义千问   (阿里云)                        │" -ForegroundColor White
Write-Host "  │  [8] 自定义 OpenAI 兼容接口                     │" -ForegroundColor White
Write-Host "  │                                                 │" -ForegroundColor White
Write-Host "  │  [0] 跳过，以后再配                             │" -ForegroundColor Gray
Write-Host "  │                                                 │" -ForegroundColor White
Write-Host "  └─────────────────────────────────────────────────┘" -ForegroundColor White
Write-Host ""

$providerChoice = Read-Host "  请选择 [0-8]"

# 默认模型映射
$providerConfig = @{
    "1" = @{ Name="Anthropic"; EnvKey="ANTHROPIC_API_KEY";  BaseUrl="";           DefaultModel="anthropic/claude-sonnet-4-6" }
    "2" = @{ Name="OpenAI";    EnvKey="OPENAI_API_KEY";     BaseUrl="";           DefaultModel="openai/gpt-4o" }
    "3" = @{ Name="Google";    EnvKey="GOOGLE_API_KEY";     BaseUrl="";           DefaultModel="google/gemini-2.5-flash" }
    "4" = @{ Name="DeepSeek";  EnvKey="OPENAI_API_KEY";     BaseUrl="https://api.deepseek.com/v1"; DefaultModel="deepseek/deepseek-chat" }
    "5" = @{ Name="OpenRouter";EnvKey="OPENROUTER_API_KEY"; BaseUrl="https://openrouter.ai/api/v1"; DefaultModel="openrouter/auto" }
    "6" = @{ Name="Kimi";      EnvKey="MOONSHOT_API_KEY";   BaseUrl="https://api.moonshot.cn/v1"; DefaultModel="moonshot/moonshot-v1-128k" }
    "7" = @{ Name="通义千问";  EnvKey="DASHSCOPE_API_KEY";  BaseUrl="https://dashscope.aliyuncs.com/compatible-mode/v1"; DefaultModel="qwen/qwen-max" }
    "8" = @{ Name="Custom";    EnvKey="OPENAI_API_KEY";     BaseUrl="";           DefaultModel="" }
}

$apiKey = ""
$baseUrl = ""
$providerName = ""
$defaultModel = ""
$skipProvider = $false

if ($providerChoice -eq "0") {
    Write-Warn "跳过模型配置，稍后运行 openclaw onboard 设置"
    $skipProvider = $true
}
elseif ($providerConfig.ContainsKey($providerChoice)) {
    $cfg = $providerConfig[$providerChoice]
    $providerName = $cfg.Name
    $baseUrl = $cfg.BaseUrl
    $defaultModel = $cfg.DefaultModel

    Write-Host ""
    Write-Host "  📌 已选择: $($cfg.Name)" -ForegroundColor Cyan

    # 如果是自定义，需要填地址
    if ($providerChoice -eq "8") {
        $baseUrl = Read-Host "  输入 API 地址 (Base URL，如 https://your-api.com/v1)"
        $defaultModel = Read-Host "  输入模型名称 (如 gpt-4o，留空用默认)"
        if ([string]::IsNullOrWhiteSpace($defaultModel)) { $defaultModel = "gpt-4o" }
    }

    # 输入 API Key
    Write-Host ""
    Write-Host "  请输入 $($cfg.Name) 的 API Key:" -ForegroundColor Yellow
    Write-Host "  (输入时不会显示，这是正常的)" -ForegroundColor DarkGray
    $secureKey = Read-Host "  API Key" -AsSecureString
    $apiKey = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)
    )

    if ([string]::IsNullOrWhiteSpace($apiKey)) {
        Write-Warn "API Key 为空，跳过模型配置"
        $skipProvider = $true
    } else {
        Write-OK "API Key 已配置"

        # 选择具体模型（可选）
        Write-Host ""
        Write-Host "  推荐模型: $defaultModel" -ForegroundColor DarkGray
        $customModel = Read-Host "  直接回车用默认，或输入自定义模型名称"
        if (-not [string]::IsNullOrWhiteSpace($customModel)) {
            $defaultModel = $customModel
        }
        Write-OK "使用模型: $defaultModel"
    }
}

# ============================================================
#  STEP 5: 写入 OpenClaw 配置
# ============================================================
Write-Step "5/8" "写入配置文件"
Write-Div

$openclawHome = "$env:USERPROFILE\.openclaw"
$configDir = $openclawHome
$configFile = "$configDir\openclaw.json"

# 确保目录存在
if (-not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Force -Path $configDir | Out-Null
}

# 如果有模型配置，写入 JSON
if (-not $skipProvider -and $providerName) {
    Write-Info "写入模型配置到 $configFile"

    # 根据 provider 构建 config JSON
    $config = @{
        env = @{}
        agents = @{
            defaults = @{
                model = @{
                    primary = $defaultModel
                }
                models = @{}
            }
        }
    }

    # 设置环境变量 Key
    if ($providerConfig.ContainsKey($providerChoice)) {
        $config.env[$providerConfig[$providerChoice].EnvKey] = $apiKey
    }

    # 如果有自定义 base URL，设置 provider 配置
    if ($baseUrl) {
        $config.models = @{
            providers = @{}
        }
        $providerSlug = $providerName.ToLower() -replace '[^a-z0-9]',''
        $config.models.providers[$providerSlug] = @{
            baseUrl = $baseUrl
        }
    }

    # 写入模型目录
    $config.agents.defaults.models[$defaultModel] = @{
        alias = $providerName
    }

    # 转为 JSON 并写入
    $json = $config | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($configFile, $json, [System.Text.Encoding]::UTF8)

    Write-OK "配置文件已写入"
    Write-Info "文件位置: $configFile"
} else {
    Write-Warn "未配置模型，跳过配置文件写入"
    Write-Info "稍后运行 openclaw onboard 进行配置"
}

# 设置系统环境变量（持久化）
if (-not $skipProvider -and $providerName -and $providerConfig.ContainsKey($providerChoice)) {
    $envKey = $providerConfig[$providerChoice].EnvKey
    [System.Environment]::SetEnvironmentVariable($envKey, $apiKey, "User")
    [Environment]::SetEnvironmentVariable($envKey, $apiKey, "Process")
    Write-OK "环境变量 $envKey 已设置 (用户级)"
}

# ============================================================
#  STEP 6: 安装实用插件
# ============================================================
Write-Step "6/8" "安装实用插件"
Write-Div

Write-Host ""
Write-Host "  以下插件已内置，无需额外安装:" -ForegroundColor Gray
Write-Host "  ✅ anthropic-provider    - Anthropic 模型" -ForegroundColor DarkGray
Write-Host "  ✅ openai-provider       - OpenAI 模型" -ForegroundColor DarkGray
Write-Host "  ✅ google-plugin         - Google/Gemini 模型" -ForegroundColor DarkGray
Write-Host "  ✅ ollama-provider       - 本地模型" -ForegroundColor DarkGray
Write-Host "  ✅ telegram              - Telegram 频道" -ForegroundColor DarkGray
Write-Host "  ✅ memory-core           - 记忆功能" -ForegroundColor DarkGray
Write-Host "  ✅ browser-plugin        - 浏览器控制" -ForegroundColor DarkGray
Write-Host ""

Write-Host "  以下插件需要额外安装:" -ForegroundColor Yellow
Write-Host "  [1] 🔍 Web Search (DuckDuckGo)     - AI 联网搜索" -ForegroundColor White
Write-Host "  [2] 🌐 Web Search (Tavily)          - 更强的联网搜索" -ForegroundColor White
Write-Host "  [3] 🧠 Memory (LanceDB)             - 持久化记忆数据库" -ForegroundColor White
Write-Host "  [4] 📄 Document Extract              - 文档内容提取" -ForegroundColor White
Write-Host "  [5] 🔊 Edge TTS (自研)              - 免费语音合成" -ForegroundColor White
Write-Host "  [6] 💬 Discord                       - Discord 频道" -ForegroundColor White
Write-Host "  [7] 📱 WhatsApp                      - WhatsApp 频道" -ForegroundColor White
Write-Host "  [8] 💚 微信                          - 微信聊天（扫码即用）" -ForegroundColor White
Write-Host "  [0] 跳过，都不装" -ForegroundColor Gray
Write-Host ""

$pluginChoice = Read-Host "  输入要装的编号 (多选用逗号分隔，如 1,3,5，或 all 装全部)"

$pluginsToInstall = @()
if ($pluginChoice -eq "all") {
    $pluginsToInstall = @("1","2","3","4","5","6","7","8")
} elseif ($pluginChoice -ne "0") {
    $pluginsToInstall = $pluginChoice -split "," | ForEach-Object { $_.Trim() }
}

$pluginMap = @{
    "1" = @{ Id="duckduckgo";     Cmd="openclaw plugins install @openclaw/duckduckgo-plugin" }
    "2" = @{ Id="tavily";         Cmd="openclaw plugins install @openclaw/tavily-plugin" }
    "3" = @{ Id="memory-lancedb"; Cmd="openclaw plugins install @openclaw/memory-lancedb" }
    "4" = @{ Id="document-extract";Cmd="openclaw plugins install @openclaw/document-extract-plugin" }
    "5" = @{ Id="edge-tts";       Cmd="LOCAL" }
    "6" = @{ Id="discord";        Cmd="openclaw plugins install @openclaw/discord" }
    "7" = @{ Id="whatsapp";       Cmd="openclaw plugins install @openclaw/whatsapp" }
    "8" = @{ Id="weixin";         Cmd="npx -y @tencent-weixin/openclaw-weixin-cli@latest install" }
}

foreach ($p in $pluginsToInstall) {
    if ($pluginMap.ContainsKey($p)) {
        $info = $pluginMap[$p]
        Write-Host ""
        Write-Host "  📦 安装 $($info.Id)..." -ForegroundColor Yellow

        if ($info.Cmd -eq "LOCAL") {
            # Edge TTS - 从 U盘本地安装
            $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
            $ttsSource = Join-Path $scriptDir "plugins\edge-tts"
            if (Test-Path $ttsSource) {
                Write-Info "使用 openclaw plugins install --link 注册插件..."
                $linkOk = Invoke-WithTimeout -TimeoutSec 60 -Label "注册 Edge TTS 插件" -ScriptBlock {
                    & openclaw plugins install --link "$ttsSource" 2>$null
                }
                if ($linkOk) {
                    Write-OK "Edge TTS 插件已注册到 OpenClaw"
                } else {
                    # fallback: 直接复制
                    Write-Warn "link 方式失败，尝试直接复制..."
                    $ttsDest = "$openclawHome\plugins\edge-tts"
                    if (-not (Test-Path $ttsDest)) {
                        New-Item -ItemType Directory -Force -Path $ttsDest | Out-Null
                    }
                    Copy-Item -Path "$ttsSource\*" -Destination $ttsDest -Recurse -Force
                    Write-OK "Edge TTS 插件已复制到 $ttsDest"
                }
                Write-Info "TTS 服务端口: 8100 (需要 Python 环境运行)"
            } else {
                Write-Err "未找到 Edge TTS 插件文件"
            }
        } else {
            # 在线安装（带超时保护，微信 npx 等可能较慢）
            $pluginTimeout = if ($info.Id -eq "weixin") { 180 } else { 120 }
            $pluginLabel = "安装 $($info.Id)"
            $pluginOk = Invoke-WithTimeout -TimeoutSec $pluginTimeout -Label $pluginLabel -ScriptBlock {
                & cmd /c "$($info.Cmd) 2>&1"
            }
            if ($pluginOk) {
                Write-OK "$($info.Id) 安装成功"
            } else {
                Write-Warn "$($info.Id) 安装超时，稍后可手动安装"
            }
        }
    }
}

if ($pluginsToInstall.Count -eq 0) {
    Write-Warn "跳过插件安装"
}

# ============================================================
#  STEP 7: 安装守护进程 & 启动 Gateway
# ============================================================
Write-Step "7/8" "安装守护进程 & 启动 Gateway"
Write-Div

# 安装守护进程（开机自启）
Write-Info "安装 Gateway 守护进程（开机自启）..."
try {
    & openclaw onboard --accept-risk --install-daemon --non-interactive 2>$null
    Write-OK "守护进程已安装"
} catch {
    Write-Warn "守护进程自动安装未完成"
    Write-Info "稍后请手动运行: openclaw onboard --install-daemon"
}

# 启动 Gateway
Write-Info "启动 Gateway 服务..."
try {
    & openclaw gateway start 2>$null
    Start-Sleep -Seconds 3
} catch {}

Write-Host ""
Write-Info "检查 Gateway 状态:"
& openclaw gateway status

# 放行防火墙（局域网访问需要）
try {
    $rule = Get-NetFirewallRule -DisplayName "OpenClaw Gateway" -ErrorAction SilentlyContinue
    if (-not $rule) {
        New-NetFirewallRule -DisplayName "OpenClaw Gateway" -Direction Inbound -Protocol TCP -LocalPort 18789 -Action Allow -Description "OpenClaw Gateway 局域网访问" -ErrorAction SilentlyContinue | Out-Null
        Write-OK "防火墙已放行端口 18789（局域网可访问）"
    }
} catch {
    Write-Info "防火墙规则设置跳过（不影响本机使用）"
}

# ============================================================
#  STEP 8: 完成
# ============================================================
Write-Step "8/8" "安装完成"
Write-Div

# 生成桌面使用指南
$desktop = [Environment]::GetFolderPath("Desktop")
$guidePath = "$desktop\OpenClaw使用指南.txt"

$guide = @"
╔══════════════════════════════════════════════════════════════╗
║            🐾  OpenClaw 使用指南  🐾                       ║
║            安装 by 小许                                     ║
╚══════════════════════════════════════════════════════════════╝

📌 快速开始
───────────
打开浏览器访问: http://localhost:18789
或运行命令: openclaw dashboard

📌 常用命令
───────────
openclaw dashboard         打开控制面板
openclaw gateway status    查看运行状态
openclaw gateway restart   重启服务
openclaw gateway stop      停止服务
openclaw gateway logs      查看日志
openclaw onboard           重新配置模型

📌 接入聊天工具
───────────
微信:  npx -y @tencent-weixin/openclaw-weixin-cli@latest install
       (运行后扫码即可)
Telegram: openclaw channel add telegram
Discord:  openclaw channel add discord
WhatsApp: openclaw channel add whatsapp

📌 模型切换
───────────
openclaw onboard               重新选择模型和 API Key

📌 已安装插件
───────────
"@

foreach ($p in $pluginsToInstall) {
    if ($pluginMap.ContainsKey($p)) {
        $guide += "`n  ✅ $($pluginMap[$p].Id)"
    }
}

$guide += @"

📌 局域网访问（其他电脑/手机用）
───────────
1. 查看本机 IP: ipconfig
2. 其他设备访问: http://<IP>:18789
3. 需要放行防火墙端口 18789

📌 常见问题
───────────
Q: openclaw 提示找不到命令？
A: 重新打开 cmd/PowerShell，或重启电脑

Q: 浏览器打不开面板？
A: openclaw gateway status  检查是否运行

Q: 重启电脑后 Gateway 没有自动启动？
A: 运行 openclaw onboard --install-daemon 安装守护进程

Q: AI 不回消息？
A: openclaw onboard  重新配置 API Key
"@

[System.IO.File]::WriteAllText($guidePath, $guide, [System.Text.Encoding]::UTF8)
Write-OK "使用指南已保存到桌面: OpenClaw使用指南.txt"

# ─── 完成 Banner ─────────────────────────────────────────────
Write-Host ""
Write-Host "  ╔══════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "  ║                                                      ║" -ForegroundColor Green
Write-Host "  ║          🎉  安装完成！ by 小许  🎉                 ║" -ForegroundColor Green
Write-Host "  ║                                                      ║" -ForegroundColor Green
Write-Host "  ╠══════════════════════════════════════════════════════╣" -ForegroundColor Green
if ($providerName -and -not $skipProvider) {
Write-Host "  ║  模型: $providerName  |  $defaultModel" -ForegroundColor Green
Write-Host "  ║  地址: http://localhost:18789                        ║" -ForegroundColor Green
} else {
Write-Host "  ║  模型: 未配置 (运行 openclaw onboard 设置)          ║" -ForegroundColor Green
Write-Host "  ║  地址: http://localhost:18789                        ║" -ForegroundColor Green
}
Write-Host "  ║                                                      ║" -ForegroundColor Green
Write-Host "  ║  💡 快速打开: openclaw dashboard                    ║" -ForegroundColor Green
Write-Host "  ║  📖 使用指南: 桌面 → OpenClaw使用指南.txt           ║" -ForegroundColor Green
Write-Host "  ║                                                      ║" -ForegroundColor Green
Write-Host "  ╚══════════════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""

$openNow = Read-Host "  按回车打开控制面板，输入 q 退出"
if ($openNow -ne "q") {
    & openclaw dashboard
}
