@echo off
chcp 65001 >nul 2>&1
title OpenClaw 快捷操作 by 小许
color 0B

:: ─── 检查 openclaw 是否可用 ──────────────────────────────
where openclaw >nul 2>&1
if %errorlevel% neq 0 (
    cls
    echo.
    echo  ╔══════════════════════════════════════════════════════╗
    echo  ║  ❌  OpenClaw 未安装或未在系统 PATH 中             ║
    echo  ║                                                    ║
    echo  ║  请先运行 setup.bat 完成安装                       ║
    echo  ║  如果已安装，请重启 cmd 或重新登录系统             ║
    echo  ╚══════════════════════════════════════════════════════╝
    echo.
    pause
    exit /b 1
)

:menu
cls
echo.
echo  ╔════════════════════════════════════════════════╗
echo  ║  🐾  OpenClaw 快捷操作 by 小许  🐾           ║
echo  ╠════════════════════════════════════════════════╣
echo  ║                                               ║
echo  ║  [1]  打开控制面板                            ║
echo  ║  [2]  查看运行状态                            ║
echo  ║  [3]  重启服务                                ║
echo  ║  [4]  查看实时日志                            ║
echo  ║  [5]  添加 Telegram                           ║
echo  ║  [6]  添加 Discord                            ║
echo  ║  [7]  添加 WhatsApp                           ║
echo  ║  [8]  重新配置模型/API Key                    ║
echo  ║  [9]  查看已安装插件                          ║
echo  ║  [10] 查看使用指南                            ║
echo  ║  [0]  退出                                    ║
echo  ║                                               ║
echo  ╚════════════════════════════════════════════════╝
echo.
set /p choice=  请选择:

if "%choice%"=="1"  goto :open_dashboard
if "%choice%"=="2"  goto :check_status
if "%choice%"=="3"  goto :restart
if "%choice%"=="4"  goto :show_logs
if "%choice%"=="5"  goto :add_telegram
if "%choice%"=="6"  goto :add_discord
if "%choice%"=="7"  goto :add_whatsapp
if "%choice%"=="8"  goto :reconfig
if "%choice%"=="9"  goto :list_plugins
if "%choice%"=="10" goto :show_guide
if "%choice%"=="0"  goto :exit
echo  ❌ 无效选项
timeout /t 2 >nul
goto :menu

:open_dashboard
start http://localhost:18789
echo.
echo  🌐 已打开浏览器访问 http://localhost:18789
timeout /t 2 >nul
goto :menu

:check_status
echo.
echo  ─── 检查运行状态 ───
openclaw gateway status
if %errorlevel% neq 0 (
    echo  ⚠️  Gateway 状态异常，可尝试选项 [3] 重启
)
echo.
pause
goto :menu

:restart
echo.
echo  ─── 正在重启 Gateway ───
openclaw gateway restart
if %errorlevel% equ 0 (
    echo  ✅ 重启命令已发送
) else (
    echo  ❌ 重启失败
)
timeout /t 3 >nul
echo.
openclaw gateway status
echo.
pause
goto :menu

:show_logs
echo.
echo  ─── 实时日志（Ctrl+C 退出） ───
openclaw gateway logs
echo.
echo  ⚡ 日志显示完毕
pause
goto :menu

:add_telegram
echo.
echo  📱 添加 Telegram Bot...
echo  (还没 Bot? 先在 Telegram 找 @BotFather)
echo.
openclaw channel add telegram
if %errorlevel% neq 0 echo  ❌ 添加失败，请检查网络后重试
pause
goto :menu

:add_discord
openclaw channel add discord
if %errorlevel% neq 0 echo  ❌ 添加失败
pause
goto :menu

:add_whatsapp
openclaw channel add whatsapp
if %errorlevel% neq 0 echo  ❌ 添加失败
pause
goto :menu

:reconfig
echo.
echo  ─── 重新配置模型/API Key ───
openclaw onboard
if %errorlevel% neq 0 echo  ❌ 配置失败
pause
goto :menu

:list_plugins
echo.
echo  ─── 已安装插件列表 ───
openclaw plugins list --enabled --verbose 2>nul
if %errorlevel% neq 0 echo  ⚠️  无法获取插件列表
echo.
pause
goto :menu

:show_guide
start "" "%~dp0使用指南.txt"
echo  📖 已打开使用指南
timeout /t 2 >nul
goto :menu

:exit
exit /b