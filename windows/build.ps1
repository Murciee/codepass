# Build SMS code -> clipboard single-file exe.
# Fluent (Windows Store style) WPF UI; tray uses System.Windows.Forms.NotifyIcon.
# Usage:  powershell -ExecutionPolicy Bypass -File build.ps1
# NOTE: keep this file ASCII-only (Windows PowerShell mis-decodes UTF-8 without BOM).

[CmdletBinding()]
param([string]$OutputDirectory = '', [switch]$SkipPackage, [string]$ReleaseDirectory = '')

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$srcs = Join-Path $root 'src\*.cs'
$dist = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { Join-Path $root 'dist' } else { [IO.Path]::GetFullPath($OutputDirectory) }
$out  = Join-Path $dist 'codepass.exe'
$icon = Join-Path $root 'codepass.ico'
$repo = Split-Path -Parent $root
$sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' -File | Sort-Object Name)
$version = [regex]::Match([IO.File]::ReadAllText((Join-Path $root 'src\Updater.cs')), 'AppVersion\s*=\s*"(\d+\.\d+\.\d+)"').Groups[1].Value
if (-not $version) { throw 'AppVersion not found' }
$documents = @(
    @{ Path = 'README.md'; Pattern = '\*\*v(\d+\.\d+\.\d+)\*\*' },
    @{ Path = 'README.en.md'; Pattern = '\*\*v(\d+\.\d+\.\d+)\*\*' },
    @{ Path = 'CHANGELOG.md'; Pattern = '(?m)^## (\d+\.\d+\.\d+)' },
    @{ Path = 'docs\RELEASE_NOTES.md'; Pattern = '(?m)^## codepass v(\d+\.\d+\.\d+)' }
)
foreach ($document in $documents) {
    $declared = [regex]::Match([IO.File]::ReadAllText((Join-Path $repo $document.Path)), $document.Pattern).Groups[1].Value
    if ($declared -ne $version) { throw ('Version mismatch: ' + $document.Path + ' expected ' + $version) }
}
$inputs = @($sourceFiles | ForEach-Object { 'src/' + $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
$inputs += 'build.ps1:' + (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
$inputs += 'codepass.ico:' + (Get-FileHash -LiteralPath $icon -Algorithm SHA256).Hash
$sha = [Security.Cryptography.SHA256]::Create()
try { $sourceHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($inputs -join "`n"))))).Replace('-','').ToLowerInvariant() }
finally { $sha.Dispose() }
$commit = 'nogit'; $dirty = $true
if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitCommit = & git -C $repo rev-parse --short=12 HEAD 2>$null
    if ($LASTEXITCODE -eq 0 -and $gitCommit -match '^[0-9a-f]{7,12}$') { $commit = [string]$gitCommit }
    $gitChanges = @(& git -C $repo status --porcelain -- windows/src windows/build.ps1 2>$null)
    if ($LASTEXITCODE -eq 0) { $dirty = $gitChanges.Count -gt 0 }
}
$builtUtc = [DateTime]::UtcNow.ToString('yyyy-MM-ddTHH:mm:ssZ')
$buildId = $version + '+' + [DateTime]::UtcNow.ToString('yyyyMMddHHmmss') + '.' + $commit + $(if ($dirty) { '.dirty' }) + '.src' + $sourceHash.Substring(0,12)

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
# 邮箱授权码加密依赖默认引用的 System.Security；此处补 UIAutomation (QQ 弹窗文字读取)
$extraNames = @('UIAutomationClient', 'UIAutomationTypes')
$refs = @('System.dll', 'System.Core.dll', 'System.Windows.Forms.dll', 'System.Drawing.dll', 'System.Runtime.Serialization.dll')
foreach ($n in $wpfNames) { $refs += (Resolve-Gac $n) }
foreach ($n in $zipNames) { $refs += (Resolve-Gac $n) }
foreach ($n in $extraNames) { $refs += (Resolve-Gac $n) }
$refArg = '/reference:' + ($refs -join ',')

New-Item -ItemType Directory -Force -Path $dist | Out-Null
$stamp = Join-Path $dist 'BuildStamp.g.cs'
$stampText = 'using System.Reflection;' + "`r`n" +
    '[assembly: AssemblyVersion("' + $version + '.0")]' + "`r`n" +
    '[assembly: AssemblyFileVersion("' + $version + '.0")]' + "`r`n" +
    '[assembly: AssemblyInformationalVersion("' + $buildId + '")]'
[IO.File]::WriteAllText($stamp, $stampText, [Text.Encoding]::UTF8)
& $csc /nologo /target:winexe /optimize+ /codepage:65001 `
    $refArg `
    $(if (Test-Path $icon) { "/win32icon:$icon" }) `
    /out:"$out" $srcs $stamp

if ($LASTEXITCODE -ne 0) { throw "build failed (exit $LASTEXITCODE)" }
$exeHash = (Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash
$metadata = [ordered]@{ version = $version; buildId = $buildId; builtUtc = $builtUtc; commit = $commit; dirty = $dirty; sourceSha256 = $sourceHash; exeSha256 = $exeHash }
[IO.File]::WriteAllText((Join-Path $dist 'build-info.json'), ($metadata | ConvertTo-Json), [Text.Encoding]::UTF8)
Write-Host ('BUILD_ID=' + $buildId)
Write-Host ('SHA256=' + $exeHash)
if ($SkipPackage) { Write-Host "OK -> $out (package skipped)"; return }

# Do not copy config.ini into the build output. It is runtime data and may
# contain tokens/history-related settings from a previous local run. The
# application creates its default configuration in memory on first launch.

# Package the portable build into release\v<version>\codepass-windows.zip.
# Entries use '/' separators: Compress-Archive on Windows PowerShell 5.1 writes
# '\' which non-Windows unzip tools treat as literal filename characters.
$releaseDir = if ([string]::IsNullOrWhiteSpace($ReleaseDirectory)) { Join-Path $root ('..\release\v' + $version) } else { [IO.Path]::GetFullPath($ReleaseDirectory) }
$zipOut = Join-Path $releaseDir 'codepass-windows.zip'
$zipTmp = "$zipOut.tmp"
$packageAllow = @(
    @{ Name = 'codepass.exe';             Source = $out },
    @{ Name = 'build-info.json';          Source = (Join-Path $dist 'build-info.json') },
    @{ Name = 'README.md';                Source = (Join-Path $root '..\README.md') },
    @{ Name = 'README.en.md';             Source = (Join-Path $root '..\README.en.md') },
    @{ Name = 'LICENSE';                  Source = (Join-Path $root '..\LICENSE') },
    @{ Name = 'DISCLAIMER.md';            Source = (Join-Path $root '..\DISCLAIMER.md') },
    @{ Name = 'PRIVACY.md';               Source = (Join-Path $root '..\PRIVACY.md') },
    @{ Name = 'SECURITY.md';              Source = (Join-Path $root '..\SECURITY.md') },
    @{ Name = 'SUPPORT.md';               Source = (Join-Path $root '..\SUPPORT.md') },
    @{ Name = 'THIRD_PARTY_NOTICES.md';    Source = (Join-Path $root '..\THIRD_PARTY_NOTICES.md') },
    @{ Name = 'assets/v1.0.4/history-search.png'; Source = (Join-Path $root '..\assets\v1.0.4\history-search.png') },
    @{ Name = 'assets/v1.0.4/email-status-narrow.png'; Source = (Join-Path $root '..\assets\v1.0.4\email-status-narrow.png') },
    @{ Name = 'assets/v1.0.4/sms-settings-narrow.png'; Source = (Join-Path $root '..\assets\v1.0.4\sms-settings-narrow.png') },
    @{ Name = 'assets/v1.0.4/privacy-settings.png'; Source = (Join-Path $root '..\assets\v1.0.4\privacy-settings.png') },
    @{ Name = 'assets/codepass-sms.png'; Source = (Join-Path $root '..\assets\codepass-sms.png') },
    @{ Name = 'docs/RELEASE_NOTES.md';     Source = (Join-Path $root '..\docs\RELEASE_NOTES.md') },
    @{ Name = 'GMAIL_APP_PASSWORD.md';   Source = (Join-Path $root '..\docs\GMAIL_APP_PASSWORD.md') },
    @{ Name = 'docs/GMAIL_APP_PASSWORD.md'; Source = (Join-Path $root '..\docs\GMAIL_APP_PASSWORD.md') },
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
