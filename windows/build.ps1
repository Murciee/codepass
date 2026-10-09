# Build SMS code -> clipboard single-file exe.
# Fluent (Windows Store style) WPF UI; tray uses System.Windows.Forms.NotifyIcon.
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1
# NOTE: keep this file ASCII-only (Windows PowerShell mis-decodes UTF-8 without BOM).

[CmdletBinding()]
param([string]$OutputDirectory = '')

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcs = Join-Path $root 'src\*.cs'
$dist = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $root 'dist' } else { [IO.Path]::GetFullPath($OutputDirectory) }
$out  = Join-Path $dist 'codepass.exe'
$icon = Join-Path $root 'codepass.ico'

if (-not (Test-Path $icon)) {
    throw "Application icon not found: $icon"
}

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw "csc.exe not found: $csc"
}

# WPF assemblies are not on the default csc search path; locate them in the GAC.
function Resolve-Gac([string]$name) {
    $roots = @(
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_MSIL'),
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_64'),
        (Join-Path $env:WINDIR 'Microsoft.NET\assembly\GAC_32')
    )
    foreach ($r in $roots) {
        $d = Join-Path $r $name
        if (Test-Path $d) {
            $f = Get-ChildItem $d -Recurse -Filter "$name.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($f) { return $f.FullName }
        }
    }
    throw "Assembly not found in GAC: $name"
}

$wpfNames = @('PresentationFramework', 'PresentationCore', 'WindowsBase', 'System.Xaml')
$zipNames = @('System.IO.Compression', 'System.IO.Compression.FileSystem')
$refs = @('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll')
foreach ($n in $wpfNames) { $refs += (Resolve-Gac $n) }
foreach ($n in $zipNames) { $refs += (Resolve-Gac $n) }
$refArg = '/reference:' + ($refs -join ',')

New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
    $refArg `
    $(if (Test-Path $icon) { "/win32icon:$icon" }) `
    /out:"$out" $srcs

if ($LASTEXITCODE -ne 0) { throw "build failed (exit $LASTEXITCODE)" }

# Do not copy config.ini into the build output. It is runtime data and may
# contain tokens/history-related settings from a previous local run. The
# application creates its default configuration in memory on first launch.

# Package the portable build into release\codepass-windows.zip.
# Entries use '/' separators: Compress-Archive on Windows PowerShell 5.1 writes
# '\' which non-Windows unzip tools treat as literal filename characters.
$releaseDir = Join-Path $root '..\release'
$zipOut = Join-Path $releaseDir 'codepass-windows.zip'
$zipTmp = "$zipOut.tmp"
$packageAllow = @(
    @{ Name = 'codepass.exe';             Source = $out },
    @{ Name = 'README.md';                Source = (Join-Path $root '..\README.md') },
    @{ Name = 'README.en.md';             Source = (Join-Path $root '..\README.en.md') },
    @{ Name = 'CHANGELOG.md';             Source = (Join-Path $root '..\CHANGELOG.md') },
    @{ Name = 'LICENSE';                  Source = (Join-Path $root '..\LICENSE') },
    @{ Name = 'DISCLAIMER.md';            Source = (Join-Path $root '..\DISCLAIMER.md') },
    @{ Name = 'PRIVACY.md';               Source = (Join-Path $root '..\PRIVACY.md') },
    @{ Name = 'SECURITY.md';              Source = (Join-Path $root '..\SECURITY.md') },
    @{ Name = 'SUPPORT.md';               Source = (Join-Path $root '..\SUPPORT.md') },
    @{ Name = 'THIRD_PARTY_NOTICES.md';    Source = (Join-Path $root '..\THIRD_PARTY_NOTICES.md') },
    @{ Name = 'docs/BUILD_ANDROID.md';     Source = (Join-Path $root '..\docs\BUILD_ANDROID.md') },
    @{ Name = 'docs/RELEASE_CHECKLIST.md'; Source = (Join-Path $root '..\docs\RELEASE_CHECKLIST.md') },
    @{ Name = 'docs/RELEASE_NOTES.md';     Source = (Join-Path $root '..\docs\RELEASE_NOTES.md') },
    @{ Name = 'phone/README.md';          Source = (Join-Path $root '..\phone\README.md') },
    @{ Name = 'phone/no-root.md';         Source = (Join-Path $root '..\phone\no-root.md') },
    @{ Name = 'phone/ntfy.md';            Source = (Join-Path $root '..\phone\ntfy.md') },
    @{ Name = 'phone/magisk/README.md';   Source = (Join-Path $root '..\phone\magisk\README.md') },
    @{ Name = 'phone/lsposed/README.md';  Source = (Join-Path $root '..\phone\lsposed\README.md') }
)
foreach ($item in $packageAllow) {
    if (-not (Test-Path -LiteralPath $item.Source -PathType Leaf)) {
        throw "Release entry not found: $($item.Source)"
    }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
# Build to a temporary file first so a failure cannot replace the last
# known-good release zip with a half-written archive.
$zipStream = [System.IO.File]::Open($zipTmp, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
try {
    $zip = New-Object System.IO.Compression.ZipArchive($zipStream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $packageAllow) {
            [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $item.Source, $item.Name) | Out-Null
        }
    } finally {
        $zip.Dispose()
    }
} finally {
    $zipStream.Dispose()
}
Move-Item -LiteralPath $zipTmp -Destination $zipOut -Force
Write-Host "release -> $zipOut" -ForegroundColor Green

Write-Host ""
Write-Host "OK -> $out" -ForegroundColor Green
Write-Host 'Package contains no local runtime config or message history.'
