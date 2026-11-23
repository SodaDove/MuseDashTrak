#requires -version 5.1
<#
    Builds MuseDashTrak.dll (MelonLoader mod) and mdt-status.exe (bridge).

    Requirements:
      - Windows with the .NET Framework 4.8 csc.exe (ships with Windows)
      - the "dotnet" host (any runtime >= 6) to run the Roslyn compiler
    Everything else (Roslyn + .NET 6 reference assemblies + MelonLoader
    assemblies) is downloaded into .\_tools on first run.
#>
param(
    [string]$Tools = "$PSScriptRoot\_tools"
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Force $dist | Out-Null

function Ensure-Dir([string]$path) {
    if (-not (Test-Path $path)) { New-Item -ItemType Directory -Force $path | Out-Null }
}

function Get-NuGet([string]$id, [string]$version, [string]$dest) {
    $zip = Join-Path $env:TEMP "$id.$version.zip"
    if (-not (Test-Path $zip)) {
        Write-Host "downloading $id $version..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg" -OutFile $zip -UseBasicParsing
    }
    Ensure-Dir $dest
    Expand-Archive $zip -DestinationPath $dest -Force
}

$csc = Join-Path $Tools 'roslyn\tasks\netcore\bincore\csc.dll'
$refDir = Join-Path $Tools 'netref\ref\net6.0'
$mlNet6 = Join-Path $Tools 'MelonLoader\net6'

if (-not (Test-Path $csc)) {
    Get-NuGet 'microsoft.net.compilers.toolset' '4.11.0' (Join-Path $Tools 'roslyn')
}
if (-not (Test-Path $refDir)) {
    Get-NuGet 'microsoft.netcore.app.ref' '6.0.36' (Join-Path $Tools 'netref')
}
if (-not (Test-Path $mlNet6)) {
    $mlZip = Join-Path $env:TEMP 'MelonLoader.x64.zip'
    if (-not (Test-Path $mlZip)) {
        Write-Host 'downloading MelonLoader 0.7.0...' -ForegroundColor Cyan
        Invoke-WebRequest -Uri 'https://github.com/LavaGang/MelonLoader/releases/download/v0.7.0/MelonLoader.x64.zip' -OutFile $mlZip -UseBasicParsing
    }
    $tmp = Join-Path $Tools '_melontmp'
    Ensure-Dir $tmp
    Expand-Archive $mlZip -DestinationPath $tmp -Force
    Ensure-Dir (Join-Path $Tools 'MelonLoader')
    Copy-Item (Join-Path $tmp 'MelonLoader\net6') $mlNet6 -Recurse -Force
    Remove-Item $tmp -Recurse -Force
}

$refs = @()
$refs += (Get-ChildItem $refDir -Filter *.dll).FullName
$refs += (Get-ChildItem $mlNet6 -Filter *.dll).FullName

$args = @($csc, '/noconfig', '/nostdlib+', '/target:library', '/langversion:latest',
          "/out:$dist\MuseDashTrak.dll")
foreach ($r in $refs) { $args += "/r:$r" }
$args += (Join-Path $root 'src\Mod.cs')
& dotnet @args
if ($LASTEXITCODE -ne 0) { throw 'mod build failed' }

& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /optimize+ /platform:x64 `
    "/out:$dist\mdt-status.exe" (Join-Path $root 'src\mdt-status.cs')
if ($LASTEXITCODE -ne 0) { throw 'bridge build failed' }

Write-Host 'build ok -> dist\' -ForegroundColor Green
