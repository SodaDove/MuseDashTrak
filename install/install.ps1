#requires -version 5.1
<#
    MuseDashTrak installer
    - installs MelonLoader into the Muse Dash folder (skipped if present)
    - copies MuseDashTrak.dll into Mods\
    - copies mdt-status.exe into %LOCALAPPDATA%\MuseDashTrak
    - adds a NekoTrak custom status item:  ${...\mdt-status.exe}

    Uninstall:  install.cmd -Uninstall
#>
[CmdletBinding()]
param(
    [string]$GamePath = '',
    [string]$MelonLoaderZip = '',
    [string]$MelonLoaderUrl = 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.0/MelonLoader.x64.zip',
    [string]$DataDir = '',
    [string]$ConfigFile = '',
    [switch]$SkipNekoTrak,
    [switch]$Uninstall
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $root 'dist'
if (-not $DataDir) { $DataDir = Join-Path $env:LOCALAPPDATA 'MuseDashTrak' }
$dataDir = $DataDir

function Write-Step($text) { Write-Host "==> $text" -ForegroundColor Cyan }
function Write-Ok($text)   { Write-Host "    $text" -ForegroundColor Green }
function Write-Warn2($text){ Write-Host "    $text" -ForegroundColor Yellow }

function Find-MuseDash {
    $candidates = @()
    foreach ($vdf in @(
        "$env:ProgramFiles(x86)\Steam\steamapps\libraryfolders.vdf",
        "$env:ProgramFiles\Steam\steamapps\libraryfolders.vdf",
        "D:\steam\steam\steamapps\libraryfolders.vdf"
    )) {
        if (Test-Path $vdf) {
            $candidates += (Get-Content $vdf -Raw | Select-String -Pattern '"path"\s+"([^"]+)"' -AllMatches).
                Matches | ForEach-Object { $_.Groups[1].Value.Replace('\\', '\') }
        }
    }
    $candidates += 'C:\Program Files (x86)\Steam\steamapps\common', 'D:\steam\steam\steamapps\common'
    foreach ($c in $candidates) {
        $p = Join-Path (Join-Path $c 'Muse Dash') 'MuseDash.exe'
        if (Test-Path $p) { return (Split-Path $p -Parent) }
    }
    return ''
}

function Get-NekoTrakConfigs {
    $base = Join-Path $env:APPDATA 'NekoTrak'
    if (-not (Test-Path $base)) { return @() }
    return @(Get-ChildItem (Join-Path $base 'users') -Directory -ErrorAction SilentlyContinue |
        ForEach-Object { Join-Path $_.FullName 'config.json' } |
        Where-Object { Test-Path $_ })
}

function Set-NekoTrakItem([string]$configPath, [string]$exePath) {
    $json = [IO.File]::ReadAllText($configPath, [Text.Encoding]::UTF8)
    $escaped = $exePath.Replace('\', '\\')

    # remove a previous copy of our item (idempotent re-install)
    $json = [regex]::Replace($json, '\s*\{\s*"type"\s*:\s*"custom"\s*,\s*"text"\s*:\s*"\$\{[^"]*mdt-status\.exe\}"\s*\},?', '')

    if ($json -notmatch '"steamSyncItems"\s*:\s*\[') {
        throw "config.json has no steamSyncItems: $configPath"
    }
    $idx = [regex]::Match($json, '"steamSyncItems"\s*:\s*\[').Index
    $bracket = $json.IndexOf('[', $idx)
    $insert = "`r`n        {`"type`":`"custom`",`"text`":`"`${$escaped}`"},"
    $json = $json.Insert($bracket + 1, $insert)

    $null = $json | ConvertFrom-Json   # validate before writing
    $backup = "$configPath.musedashtrak.bak"
    if (-not (Test-Path $backup)) { Copy-Item $configPath $backup -Force }
    [IO.File]::WriteAllText($configPath, $json, (New-Object Text.UTF8Encoding($false)))
}

function Install-MelonLoader([string]$game) {
    if ($MelonLoaderZip -and (Test-Path $MelonLoaderZip)) {
        Expand-Archive $MelonLoaderZip -DestinationPath $game -Force
        return
    }
    $cached = Join-Path $dist 'MelonLoader.x64.zip'
    if (-not (Test-Path $cached)) {
        Write-Step 'downloading MelonLoader 0.7.0'
        New-Item -ItemType Directory -Force (Split-Path $cached) | Out-Null
        Invoke-WebRequest -Uri $MelonLoaderUrl -OutFile $cached -UseBasicParsing
    }
    Expand-Archive $cached -DestinationPath $game -Force
}

if ($Uninstall) {
    Write-Step 'uninstalling MuseDashTrak'
    if (-not $GamePath) { $GamePath = Find-MuseDash }
    if ($GamePath) {
        $dll = Join-Path $GamePath 'Mods\MuseDashTrak.dll'
        if (Test-Path $dll) { Remove-Item $dll -Force; Write-Ok "removed $dll" }
    }
    foreach ($cfg in Get-NekoTrakConfigs) {
        $json = [IO.File]::ReadAllText($cfg, [Text.Encoding]::UTF8)
        $new = [regex]::Replace($json, '\s*\{\s*"type"\s*:\s*"custom"\s*,\s*"text"\s*:\s*"\$\{[^"]*mdt-status\.exe\}"\s*\},?', '')
        if ($new -ne $json) {
            [IO.File]::WriteAllText($cfg, $new, (New-Object Text.UTF8Encoding($false)))
            Write-Ok "removed status item from $cfg"
        }
    }
    Write-Warn2 'MelonLoader itself was kept (other mods may use it). Delete version.dll and the MelonLoader folder to remove it.'
    exit 0
}

if (Get-Process -Name MuseDash -ErrorAction SilentlyContinue) {
    throw 'Muse Dash is running. Close the game before installing.'
}
if (Get-Process -Name NekoTrak -ErrorAction SilentlyContinue) {
    Write-Warn2 'NekoTrak is running: it may write its old config back on exit. Quit NekoTrak first if the status item disappears.'
}

if (-not $GamePath) { $GamePath = Find-MuseDash }
if (-not $GamePath -or -not (Test-Path (Join-Path $GamePath 'MuseDash.exe'))) {
    throw 'Muse Dash not found. Pass -GamePath "D:\...\Muse Dash".'
}
Write-Step "game: $GamePath"

if (-not (Test-Path (Join-Path $GamePath 'MelonLoader'))) {
    Install-MelonLoader $GamePath
    Write-Ok 'MelonLoader installed'
} else {
    Write-Ok 'MelonLoader already present, skipped'
}

Write-Step 'installing MuseDashTrak.dll'
$mods = Join-Path $GamePath 'Mods'
if (-not (Test-Path $mods)) { New-Item -ItemType Directory -Path $mods | Out-Null }
Copy-Item (Join-Path $dist 'MuseDashTrak.dll') (Join-Path $mods 'MuseDashTrak.dll') -Force
Write-Ok (Join-Path $mods 'MuseDashTrak.dll')

Write-Step 'installing status bridge'
if (-not (Test-Path $dataDir)) { New-Item -ItemType Directory -Path $dataDir | Out-Null }
$statusExe = Join-Path $dataDir 'mdt-status.exe'
Copy-Item (Join-Path $dist 'mdt-status.exe') $statusExe -Force
$formatDst = Join-Path $dataDir 'format.txt'
if (-not (Test-Path $formatDst)) { Copy-Item (Join-Path $dist 'format.txt') $formatDst -Force }
Write-Ok $statusExe

if (-not $SkipNekoTrak) {
    Write-Step 'adding the NekoTrak custom status item'
    $configs = if ($ConfigFile) { @($ConfigFile) } else { Get-NekoTrakConfigs }
    if ($configs.Count -eq 0) {
        Write-Warn2 ('no NekoTrak config found - add this manually:  ${' + $statusExe + '}')
    }
    foreach ($cfg in $configs) {
        Set-NekoTrakItem $cfg $statusExe
        Write-Ok "updated $cfg"
    }
}

Write-Host ''
Write-Host 'Done.' -ForegroundColor Green
Write-Host '1) restart NekoTrak (it rewrites config on exit, so quit it first)' -ForegroundColor Gray
Write-Host '2) start Muse Dash via Steam and play a song' -ForegroundColor Gray
Write-Host '3) Steam/Discord presence should show:  🔥 Song Lv.9 98.42% 129880 · 258x' -ForegroundColor Gray
Write-Host ''
Write-Host 'The first launch generates IL2CPP assemblies and can take 1-3 minutes.' -ForegroundColor DarkYellow
