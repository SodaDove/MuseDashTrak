#requires -version 5.1
<#
    MuseDashTrak 自检：确认插件、桥接程序、NekoTrak 配置和最新状态是否就绪。
    出了问题请把本脚本的输出发给我。
#>
[CmdletBinding()]
param(
    [string]$GamePath = ''
)

$ErrorActionPreference = 'Continue'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$root = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $env:LOCALAPPDATA 'MuseDashTrak'

function Line($k, $v, $ok) {
    $color = if ($ok -eq $null) { 'Gray' } elseif ($ok) { 'Green' } else { 'Red' }
    Write-Host ("{0,-28} {1}" -f $k, $v) -ForegroundColor $color
}

Write-Host '== MuseDashTrak 自检 ==' -ForegroundColor Cyan

if (-not $GamePath) {
    $GamePath = 'D:\steam\steam\steamapps\common\Muse Dash'
}
$GamePath = $GamePath.TrimEnd('\')
Line '游戏目录' $GamePath (Test-Path (Join-Path $GamePath 'MuseDash.exe'))
Line 'MelonLoader' (Test-Path (Join-Path $GamePath 'MelonLoader')) (Test-Path (Join-Path $GamePath 'MelonLoader'))
Line '插件 DLL' (Test-Path (Join-Path $GamePath 'Mods\MuseDashTrak.dll')) (Test-Path (Join-Path $GamePath 'Mods\MuseDashTrak.dll'))
Line '桥接程序' (Test-Path (Join-Path $dataDir 'mdt-status.exe')) (Test-Path (Join-Path $dataDir 'mdt-status.exe'))

$statusPath = Join-Path $dataDir 'status.json'
$hasStatus = Test-Path $statusPath
Line 'status.json' $statusPath $hasStatus

if ($hasStatus) {
    $status = [IO.File]::ReadAllText($statusPath, [Text.Encoding]::UTF8)
    Write-Host ''
    Write-Host '当前状态：' -ForegroundColor Cyan
    Write-Host $status
    $now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
    $m = [regex]::Match($status, '"updated"\s*:\s*(\d+)')
    if ($m.Success) {
        $age = $now - [long]$m.Groups[1].Value
        Line '数据年龄' "$age 秒" ($age -lt 30)
    }

    $exe = Join-Path $dataDir 'mdt-status.exe'
    if (Test-Path $exe) {
        Write-Host ''
        Write-Host 'NekoTrak 会显示：' -ForegroundColor Cyan
        $out = & $exe
        if ($out) { Write-Host "  $out" -ForegroundColor White } else { Write-Host '  （空 - 没在打歌或数据过期）' -ForegroundColor DarkGray }
    }
}

Write-Host ''
Write-Host 'NekoTrak 自定义状态项：' -ForegroundColor Cyan
$found = $false
Get-ChildItem (Join-Path $env:APPDATA 'NekoTrak\users') -Directory -ErrorAction SilentlyContinue | ForEach-Object {
    $cfg = Join-Path $_.FullName 'config.json'
    if (Test-Path $cfg) {
        $txt = [IO.File]::ReadAllText($cfg, [Text.Encoding]::UTF8)
        if ($txt -match 'mdt-status\.exe') {
            $found = $true
            Write-Host "  已配置：$cfg" -ForegroundColor Green
        }
    }
}
if (-not $found) { Write-Host '  未找到（运行 install.ps1 写入，或手动添加 ${...mdt-status.exe}）' -ForegroundColor Yellow }

Write-Host ''
Write-Host 'MelonLoader 日志：' -ForegroundColor Cyan
$logs = @()
$logs += Get-ChildItem (Join-Path $GamePath 'MelonLoader') -Recurse -Filter *.log -ErrorAction SilentlyContinue
$logs += Get-ChildItem $GamePath -Filter 'Latest.log' -ErrorAction SilentlyContinue
if ($logs.Count -eq 0) {
    Write-Host '  还没有日志 —— 说明装了 MelonLoader 后还没通过 Steam 启动过游戏。' -ForegroundColor Yellow
} else {
    foreach ($l in $logs | Sort-Object LastWriteTime -Descending | Select-Object -First 1) {
        Write-Host "  $($l.FullName)  ($($l.Length) bytes, $($l.LastWriteTime))" -ForegroundColor Gray
        Get-Content $l.FullName -Tail 25 | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkGray }
    }
}
