@echo off
chcp 65001 >nul
setlocal
set GAME=D:\steam\steam\steamapps\common\Muse Dash
set SRC=%~dp0dist\MuseDashTrak.dll

echo 等待 Muse Dash 退出（现在就可以去关游戏）...
:wait
tasklist /FI "IMAGENAME eq MuseDash.exe" 2>nul | find /I "MuseDash.exe" >nul
if not errorlevel 1 (
    timeout /t 3 /nobreak >nul
    goto wait
)

echo 游戏已退出，正在替换插件...
copy /Y "%SRC%" "%GAME%\Mods\MuseDashTrak.dll" >nul
if errorlevel 1 (
    echo 替换失败：请确认游戏完全退出后重试。
    pause
    exit /b 1
)
echo 完成。现在可以从 Steam 启动游戏了。
timeout /t 3 /nobreak >nul
