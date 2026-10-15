<#
.SYNOPSIS
    AeroFence Automated Build & Packaging Script
.DESCRIPTION
    Builds the core application, packages the payload, and produces a single-file installer in the dist/ folder.
#>

[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "             AeroFence Build System (NET 9)               " -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Verify .NET SDK
$dotnetVersion = dotnet --version
Write-Host "[1/4] Found .NET SDK: $dotnetVersion" -ForegroundColor Green

# 2. Build & Publish Core Application
Write-Host "[2/4] Publishing AeroFence ($Configuration, win-x64)..." -ForegroundColor Yellow
$appProject = Join-Path $ScriptDir "src\AeroFence\AeroFence.csproj"
$publishDir = Join-Path $ScriptDir "src\AeroFence\bin\$Configuration\net9.0-windows\publish"

dotnet publish $appProject -c $Configuration -r win-x64 --self-contained false -o $publishDir | Out-Host

if (-not (Test-Path $publishDir)) {
    throw "Publish failed: $publishDir does not exist."
}

if ($SkipInstaller) {
    Write-Host "Build complete (Skipping installer step)." -ForegroundColor Green
    exit 0
}

# 3. Create Payload Archive
Write-Host "[3/4] Creating Installer Payload ZIP..." -ForegroundColor Yellow
$payloadDir = Join-Path $ScriptDir "src\AeroFence.Installer\Resources"
if (-not (Test-Path $payloadDir)) {
    New-Item -ItemType Directory -Path $payloadDir -Force | Out-Null
}
$payloadZip = Join-Path $payloadDir "AeroFence_Payload.zip"
if (Test-Path $payloadZip) {
    Remove-Item $payloadZip -Force
}

Compress-Archive -Path "$publishDir\*" -DestinationPath $payloadZip -Force
Write-Host "      Payload compressed: $((Get-Item $payloadZip).Length / 1MB | ForEach-Object { '{0:N2} MB' -f $_ })" -ForegroundColor Gray

# 4. Publish Single-File Installer
Write-Host "[4/4] Building Single-File Installer..." -ForegroundColor Yellow
$installerProject = Join-Path $ScriptDir "src\AeroFence.Installer\AeroFence.Installer.csproj"
$distDir = Join-Path $ScriptDir "dist"
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}

dotnet publish $installerProject -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=true -o $distDir | Out-Host

$finalSetup = Join-Path $distDir "AeroFence_Setup.exe"
if (Test-Path $finalSetup) {
    Write-Host "==========================================================" -ForegroundColor Green
    Write-Host " BUILD SUCCEEDED!" -ForegroundColor Green
    Write-Host " Output binary: $finalSetup ($((Get-Item $finalSetup).Length / 1MB | ForEach-Object { '{0:N2} MB' -f $_ }))" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Green
} else {
    throw "Failed to locate generated installer executable at $finalSetup"
}
