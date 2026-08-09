@echo off
chcp 65001 >nul 2>&1
setlocal enabledelayedexpansion
title OpenClaw 一键卸载 by 小许
color 0C

echo.
echo  ╔══════════════════════════════════════════════════════╗
echo  ║      🗑️  OpenClaw 一键卸载 by 小许  🗑️            ║
echo  ║                                                      ║
echo  ║      会清除: Gateway 服务 + OpenClaw + 配置 + 插件  ║
echo  ║      ⚠️  Node.js 不会删除                           ║
echo  ╚══════════════════════════════════════════════════════╝
echo.
set /p confirm="  确定卸载吗？(y/N): "
if /i not "%confirm%"=="y" (
    echo  已取消
    exit /b
)

echo.
echo [1/6] 🛑 停止 Gateway 服务...
openclaw gateway stop 2>nul
if %errorlevel% equ 0 (echo   ✅ 服务已停止) else (echo   ℹ️  服务未在运行)

echo.
echo [2/6] 🗑️  卸载 OpenClaw...
where npm >nul 2>&1
if %errorlevel% equ 0 (
    echo   运行 npm uninstall（最多等 60 秒）...
    start /wait npm uninstall -g openclaw 2>nul
) else (
    echo   ℹ️  npm 不可用，跳过（OpenClaw 文件可能残留，可手动删除）
)
echo   ✅ 卸载命令已执行

echo.
echo [3/6] 🧹 清理配置文件...
set "OPENCLAW_HOME=%USERPROFILE%\.openclaw"
if exist "%OPENCLAW_HOME%" (
    echo   📁 配置目录: %OPENCLAW_HOME%
    echo   内容:
    dir /b "%OPENCLAW_HOME%" 2>nul
    echo.
    set /p "delcfg=   删除整个配置目录？(y/N): "
    if /i "!delcfg!"=="y" (
        rmdir /s /q "%OPENCLAW_HOME%" 2>nul
        echo   ✅ 配置目录已删除
    ) else (echo   ℹ️  保留配置目录)
) else (echo   ℹ️  配置目录不存在)

:: 也清理 AppData 里的
if exist "%APPDATA%\openclaw" (rmdir /s /q "%APPDATA%\openclaw" 2>nul & echo   ✅ 已清理 AppData\openclaw)
if exist "%LOCALAPPDATA%\openclaw" (rmdir /s /q "%LOCALAPPDATA%\openclaw" 2>nul & echo   ✅ 已清理 LocalAppData\openclaw)

echo.
echo [4/6] 🔐 清理环境变量...
for %%k in (ANTHROPIC_API_KEY OPENAI_API_KEY GOOGLE_API_KEY OPENROUTER_API_KEY MOONSHOT_API_KEY DASHSCOPE_API_KEY) do (
    reg delete "HKCU\Environment" /v %%k /f >nul 2>&1
)
echo   ✅ 已清理 API Key 环境变量

echo.
echo [5/6] 🖥️ 清理桌面...
if exist "%USERPROFILE%\Desktop\OpenClaw使用指南.txt" (
    del /f /q "%USERPROFILE%\Desktop\OpenClaw使用指南.txt" 2>nul
    echo   ✅ 已删除: OpenClaw使用指南.txt
)

echo.
echo [6/6] 🔍 验证卸载...
where openclaw >nul 2>&1
if %errorlevel% neq 0 (echo   ✅ 卸载干净) else (echo   ⚠️  重启后可能完全清除)

echo.
echo  ══════════════════════════════════════════════════════
echo  🗑️  卸载完成！
echo  📌 保留: Node.js (其他项目可能要用)
echo  📌 删除: OpenClaw + 配置 + 插件 + 环境变量 + 桌面指南
echo  ══════════════════════════════════════════════════════
echo.
pause
