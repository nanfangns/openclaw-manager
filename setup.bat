@echo off
chcp 65001 >nul 2>&1
title OpenClaw 一键安装 by 小许
color 0A

echo.
echo  ╔══════════════════════════════════════════════════════╗
echo  ║        🐾  OpenClaw 一键安装 by 小许  🐾           ║
echo  ║        按任意键开始...                              ║
echo  ╚══════════════════════════════════════════════════════╝
echo.
pause >nul

:: 获取脚本所在目录（兼容中文路径）
set "SCRIPT_DIR=%~dp0"

:: 用 PowerShell 执行主安装脚本
powershell.exe -ExecutionPolicy Bypass -NoProfile -File "%SCRIPT_DIR%install.ps1"

if %errorlevel% neq 0 (
    echo.
    echo  ❌ 安装出错，请截图联系小许
    echo.
    pause
)
