# ============================================================
#  OpenClaw 安装包 - 构建工具
#  把 E:\install-openclaw\ 打包成一个独立的 EXE 安装程序
#  使用 Windows 内置的 .NET Framework C# 编译器
#  （不需要安装任何额外工具）
# ============================================================
param(
    [string]$SourceDir = "E:\install-openclaw",
    [string]$OutputExe = "E:\OpenClawSetup.exe"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

function Write-OK   { param($t) Write-Host "  [OK] $t" -ForegroundColor Green }
function Write-Step { param($n,$t) Write-Host "`n[$n] " -NoNewline -ForegroundColor Cyan; Write-Host $t -ForegroundColor White }
function Write-Warn { param($t) Write-Host "  [!] $t" -ForegroundColor Yellow }
function Write-Info { param($t) Write-Host "  [i] $t" -ForegroundColor Gray }

# ── 1. 检查源目录 ──────────────────────────────────────────────
Write-Step "1/4" "Check source files"
if (-not (Test-Path $SourceDir)) { Write-Warn "Source dir not found: $SourceDir"; exit 1 }
$files = Get-ChildItem $SourceDir -File
$names = $files | ForEach-Object { $_.Name }
if ($names -notcontains "setup.bat") { Write-Warn "setup.bat missing"; exit 1 }
if ($names -notcontains "install.ps1") { Write-Warn "install.ps1 missing"; exit 1 }
Write-OK "$($files.Count) files in source directory"

# ── 2. 打包文件到 ZIP ──────────────────────────────────────────
Write-Step "2/4" "Package files into ZIP"
$tmp = [System.IO.Path]::GetTempPath() + [System.IO.Path]::GetRandomFileName().Replace(".","")
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
# 拷贝根目录文件（排除构建脚本）
Get-ChildItem $SourceDir -File | Where-Object {
    $_.Name -ne "build-installer.ps1" -and $_.Name -ne "installer.cs"
} | ForEach-Object { Copy-Item $_.FullName -Destination (Join-Path $tmp $_.Name) }
# 拷贝子目录
if (Test-Path (Join-Path $SourceDir "plugins")) { Copy-Item (Join-Path $SourceDir "plugins") $tmp -Recurse -Force }
if (Test-Path (Join-Path $SourceDir "templates")) { Copy-Item (Join-Path $SourceDir "templates") $tmp -Recurse -Force }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zipPath = [System.IO.Path]::GetTempPath() + "oc-archive.zip"
[System.IO.Compression.ZipFile]::CreateFromDirectory($tmp, $zipPath)
$zipBytes = [System.IO.File]::ReadAllBytes($zipPath)
$b64 = [System.Convert]::ToBase64String($zipBytes)
Write-OK "ZIP: $($zipBytes.Length) bytes, Base64: $($b64.Length) chars"

# ── 3. 生成 C# 源码（嵌入 base64 数据）─────────────────────────
Write-Step "3/4" "Generate C# source with embedded data"

$csPath = [System.IO.Path]::GetTempPath() + "oc-installer.cs"
$code = @"
using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Windows.Forms;
using System.Security.Principal;
class P {
static readonly string B = "B64DATA";
[STAThread] static void Main() {
Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
WindowsIdentity id = WindowsIdentity.GetCurrent();
WindowsPrincipal pp = new WindowsPrincipal(id);
if (!pp.IsInRole(WindowsBuiltInRole.Administrator)) {
ProcessStartInfo x = new ProcessStartInfo();
x.UseShellExecute = true;
x.FileName = Application.ExecutablePath;
x.Verb = "runas";
try { Process.Start(x); }
catch { MessageBox.Show("Need admin rights. Right-click Run as admin.", "OpenClaw Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
return;
}
if (MessageBox.Show("Install OpenClaw AI Assistant?", "OpenClaw Installer", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
try {
string g = Guid.NewGuid().ToString("N");
string d = Path.Combine(Path.GetTempPath(), "oc-" + g);
Directory.CreateDirectory(d);
byte[] a = Convert.FromBase64String(B);
string zp = Path.Combine(d, "p.zip");
File.WriteAllBytes(zp, a);
ZipFile.ExtractToDirectory(zp, d);
File.Delete(zp);
string s = Path.Combine(d, "setup.bat");
if (!File.Exists(s)) { MessageBox.Show("Package corrupted.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
try {
string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
string bat = Path.Combine(d, "快捷操作.bat");
string lnk = Path.Combine(desk, "OpenClaw.lnk");
if (File.Exists(bat) && !File.Exists(lnk)) {
Type t = Type.GetTypeFromProgID("WScript.Shell");
object sh = Activator.CreateInstance(t);
object sc = t.InvokeMember("CreateShortcut", System.Reflection.BindingFlags.InvokeMethod, null, sh, new object[] { lnk });
t.InvokeMember("TargetPath", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { bat });
t.InvokeMember("WorkingDirectory", System.Reflection.BindingFlags.SetProperty, null, sc, new object[] { d });
t.InvokeMember("Save", System.Reflection.BindingFlags.InvokeMethod, null, sc, null);
}
} catch { }
ProcessStartInfo pi = new ProcessStartInfo();
pi.FileName = "cmd.exe";
pi.Arguments = "/c cd /d \"" + d + "\" && call setup.bat && pause";
pi.UseShellExecute = false;
pi.WorkingDirectory = d;
Process sp = Process.Start(pi);
sp.WaitForExit();
try { Directory.Delete(d, true); } catch { }
if (sp.ExitCode == 0) MessageBox.Show("Install complete!\n\nDashboard: http://localhost:18789\nDesktop shortcut: OpenClaw.lnk", "OpenClaw Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
else MessageBox.Show("Installer completed with exit code: " + sp.ExitCode + ". Check the command window for details.", "OpenClaw Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
} catch (Exception ex) { MessageBox.Show("Error: " + ex.Message, "OpenClaw Installer", MessageBoxButtons.OK, MessageBoxIcon.Error); }
}
}
"@
$code = $code.Replace('"B64DATA"', '"' + $b64 + '"')
[System.IO.File]::WriteAllText($csPath, $code, [System.Text.Encoding]::ASCII)
Write-OK "C# source generated ($((Get-Item $csPath).Length) bytes)"

# ── 4. 编译 EXE ────────────────────────────────────────────────
Write-Step "4/4" "Compile executable..."

$csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { Write-Warn "C# compiler not found"; exit 1 }

$refs = @(
    "/target:winexe", "/platform:anycpu", "/nologo",
    "/out:$OutputExe",
    "/reference:System.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:System.IO.Compression.dll",
    "/reference:System.IO.Compression.FileSystem.dll",
    $csPath
)
& $csc $refs 2>&1

if (Test-Path $OutputExe) {
    $sizeKB = [math]::Round((Get-Item $OutputExe).Length / 1KB)
    Write-OK "EXE created: $OutputExe (${sizeKB}KB)"
} else {
    Write-Warn "Compilation failed"
    exit 1
}

# 清理临时文件
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $zipPath -Force -ErrorAction SilentlyContinue
Remove-Item $csPath -Force -ErrorAction SilentlyContinue

Write-Host ""
Write-Host "  ====================================================" -ForegroundColor Green
Write-Host "   Done! Output: $OutputExe" -ForegroundColor Green
Write-Host "  ====================================================" -ForegroundColor Green