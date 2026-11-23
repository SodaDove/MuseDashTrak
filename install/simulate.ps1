#requires -version 5.1
# 写一条假的“打歌中”状态，用来单独验证 NekoTrak 那边是否正常显示。
# 20 秒后自动过期（桥接程序会输出空）。
[CmdletBinding()]
param(
    [string]$Song = '曖昧サイボーグ',
    [string]$Level = '9',
    [int]$Diff = 3,
    [int]$Score = 129880,
    [double]$Acc = 98.42,
    [int]$Combo = 258
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$dir = Join-Path $env:LOCALAPPDATA 'MuseDashTrak'
New-Item -ItemType Directory -Force $dir | Out-Null
$now = [DateTimeOffset]::UtcNow.ToUnixTimeSeconds()
$songJson = $Song.Replace('\', '\\').Replace('"', '\"')
$json = '{"mod":"simulate","state":"playing","song":"' + $songJson + '","author":"","diff":' + $Diff +
        ',"level":"' + $Level + '","score":' + $Score + ',"acc":' + $Acc.ToString([Globalization.CultureInfo]::InvariantCulture) +
        ',"combo":' + $Combo + ',"perfect":400,"great":2,"early":1,"late":0,"miss":0,"elapsed":95.0,"updated":' + $now + '}'
[IO.File]::WriteAllText((Join-Path $dir 'status.json'), $json, (New-Object Text.UTF8Encoding($false)))

Write-Host '已写入模拟状态。NekoTrak 下一次刷新时应该显示：' -ForegroundColor Cyan
& (Join-Path $dir 'mdt-status.exe')
Write-Host ''
Write-Host '（20 秒后自动变空；重新启动 NekoTrak 才能保证它读到了新的自定义状态项）' -ForegroundColor DarkGray
