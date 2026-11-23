#requires -version 5.1
# 打包发布 zip：dist 里的二进制 + 安装脚本 + 文档
# 用法: powershell -ExecutionPolicy Bypass -File .\release.ps1 -Version 0.1.0
param(
    [string]$Version = '0.1.0'
)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1')

$stage = Join-Path $root "release\MuseDashTrak-v$Version"
$zip = Join-Path $root "release\MuseDashTrak-v$Version.zip"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $stage 'dist') | Out-Null

Copy-Item (Join-Path $root 'dist\MuseDashTrak.dll') (Join-Path $stage 'dist') -Force
Copy-Item (Join-Path $root 'dist\mdt-status.exe') (Join-Path $stage 'dist') -Force
Copy-Item (Join-Path $root 'dist\format.txt') (Join-Path $stage 'dist') -Force
Copy-Item (Join-Path $root 'install') (Join-Path $stage 'install') -Recurse -Force
Copy-Item (Join-Path $root 'README.md') $stage -Force
Copy-Item (Join-Path $root 'README.zh-CN.md') $stage -Force
Copy-Item (Join-Path $root 'LICENSE') $stage -Force
Copy-Item (Join-Path $root 'CHANGELOG.md') $stage -Force
Copy-Item (Join-Path $root '*.cmd') $stage -Force
if (Test-Path (Join-Path $root 'docs')) { Copy-Item (Join-Path $root 'docs') $stage -Recurse -Force }

if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -CompressionLevel Optimal
Write-Host "release: $zip" -ForegroundColor Green
