# Read-only pre-publish checks for codepass.
# Does not use the network, does not modify anything, and never prints secret values.
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-public-release.ps1
# Exit code: 0 = no blockers, 1 = blockers found.
# NOTE: keep this file ASCII-only (Windows PowerShell mis-decodes UTF-8 without BOM).

[CmdletBinding()]
param(
    [string]$Root = '',
    [switch]$RequireArtifacts
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Root)) {
    $Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
}
$Root = [System.IO.Path]::GetFullPath($Root)

$script:passed = New-Object System.Collections.ArrayList
$script:warns = New-Object System.Collections.ArrayList
$script:blocks = New-Object System.Collections.ArrayList

function Add-Pass([string]$m) { $script:passed.Add($m) | Out-Null }
function Add-Warn([string]$m) { $script:warns.Add($m) | Out-Null }
function Add-Block([string]$m) { $script:blocks.Add($m) | Out-Null }

Write-Host ''
Write-Host 'codepass pre-publish check (read-only)' -ForegroundColor Cyan
Write-Host "root: $Root"
Write-Host ''

# ------------------------------------------------------------------ options
# Directories never scanned: generated output, local caches and tool chains.
$excludeDirNames = @(
    '.git', '.dart_tool', '.gradle', '.idea', '.vscode', '.openchamber',
    'build', 'dist', 'backups', 'archive', ('_' + [char]0x5F52 + [char]0x6863), 'node_modules', 'ephemeral',
    'android-env', 'release', 'gradle-home'
)

# Signing material: never acceptable in the source tree.
$signingNames = @('key.properties', 'secrets.properties', '.env')
$signingExtensions = @('.keystore', '.jks', '.p12', '.pfx', '.pem', '.key', '.der')

# Files that may be runtime data or a harmless template: inspect contents.
$dataNames = @('config.ini', 'config.conf', 'lsposed.conf', 'history.txt', 'local.properties')

# Secret keys inside a configuration template.
$secretKeys = @('token', 'ntfy_token', 'lock_hash', 'lock_salt')

function Get-TreeFiles([string]$dir) {
    $results = New-Object System.Collections.ArrayList
    Add-TreeFilesInto $dir $results
    return ,$results
}

function Add-TreeFilesInto([string]$dir, [System.Collections.ArrayList]$into) {
    foreach ($item in Get-ChildItem -LiteralPath $dir -Force -ErrorAction SilentlyContinue) {
        if ($item.PSIsContainer) {
            if ($excludeDirNames -contains $item.Name) { continue }
            Add-TreeFilesInto $item.FullName $into
        } else {
            $into.Add($item) | Out-Null
        }
    }
}

function Get-NonEmptySecretKeys([string]$path) {
    $found = New-Object System.Collections.ArrayList
    try {
        foreach ($line in Get-Content -LiteralPath $path -ErrorAction Stop) {
            $trimmed = $line.Trim()
            if ($trimmed.Length -eq 0 -or $trimmed.StartsWith('#') -or $trimmed.StartsWith(';')) { continue }
            $split = $trimmed.IndexOf('=')
            if ($split -le 0) { continue }
            $key = $trimmed.Substring(0, $split).Trim().ToLowerInvariant()
            $value = $trimmed.Substring($split + 1).Trim().Trim('"')
            if (($secretKeys -contains $key) -and -not [string]::IsNullOrWhiteSpace($value)) {
                $found.Add($key) | Out-Null
            }
        }
    } catch {
        return $found
    }
    return $found
}

# -------------------------------------------------------------- 1. documents
$requiredDocs = @(
    'LICENSE', 'DISCLAIMER.md', 'PRIVACY.md', 'SECURITY.md',
    'SUPPORT.md', 'THIRD_PARTY_NOTICES.md', 'CHANGELOG.md', 'README.md',
    'docs\RELEASE_CHECKLIST.md', 'docs\RELEASE_NOTES.md'
)
foreach ($doc in $requiredDocs) {
    if (Test-Path -LiteralPath (Join-Path $Root $doc) -PathType Leaf) {
        Add-Pass "document present: $doc"
    } else {
        Add-Block "missing required document: $doc"
    }
}

# ----------------------------------------------------------- 2. license text
$licensePath = Join-Path $Root 'LICENSE'
if (Test-Path -LiteralPath $licensePath -PathType Leaf) {
    $license = Get-Content -LiteralPath $licensePath -Raw
    if ($license -match 'MIT License' -and $license -match 'WITHOUT WARRANTY OF ANY KIND') {
        Add-Pass 'LICENSE looks like a standard MIT license'
    } else {
        Add-Warn 'LICENSE exists but was not recognized as the expected MIT text'
    }
}

# --------------------------------------------- 3. sensitive files in sources
$allFiles = Get-TreeFiles $Root
$sensitiveCount = 0
foreach ($entry in $allFiles) {
    $name = $entry.Name.ToLowerInvariant()
    $ext = $entry.Extension.ToLowerInvariant()
    $relative = $entry.FullName.Substring($Root.Length).TrimStart('\')

    if (($signingNames -contains $name) -or ($signingExtensions -contains $ext)) {
        Add-Block "signing material found in source tree (do not commit): $relative"
        $sensitiveCount++
        continue
    }

    if ($ext -eq '.log') {
        Add-Warn "log file present (keep it out of the repository): $relative"
        continue
    }

    if ($dataNames -contains $name) {
        if ($name -eq 'history.txt') {
            if ($entry.Length -gt 0) {
                Add-Block "message history file has content (do not commit): $relative"
                $sensitiveCount++
            }
            continue
        }
        $keys = @(Get-NonEmptySecretKeys $entry.FullName)
        if ($keys.Count -gt 0) {
            Add-Block "config file contains non-empty secrets ($($keys -join ', ')): $relative"
            $sensitiveCount++
        } else {
            Add-Warn "local config or template present (ok if it is a template): $relative"
        }
    }
}
if ($sensitiveCount -eq 0) {
    Add-Pass 'no committed-looking secrets found in the source tree'
}
if ($allFiles.Count -gt 0) {
    Add-Pass "scanned $($allFiles.Count) source files"
}

# ------------------------------------------ 4. example configs must stay empty
$templates = @('phone\lsposed\config.example', 'phone\magisk\config.example')
foreach ($template in $templates) {
    $path = Join-Path $Root $template
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        Add-Warn "example config not found: $template"
        continue
    }
    $keys = @(Get-NonEmptySecretKeys $path)
    if ($keys.Count -gt 0) {
        Add-Block "example config has non-empty secrets ($($keys -join ', ')) in $template"
    } else {
        Add-Pass "example config keeps secrets empty: $template"
    }
}

# ------------------------------------------------- 5. release artifact review
$releaseDir = Join-Path $Root 'release'
if (Test-Path -LiteralPath $releaseDir -PathType Container) {
    $apks = @(Get-ChildItem -LiteralPath $releaseDir -File -Filter '*.apk' -ErrorAction SilentlyContinue)
    if ($apks.Count -eq 0) { Add-Warn 'no signed APK staged in release; Android signing is still required' }
    foreach ($apk in $apks) {
        if ($apk.Name -match 'debug|unsigned') {
            Add-Block "development APK must be moved out of release: $($apk.Name)"
        } else {
            Add-Warn "confirm the release APK is signed with the fixed certificate: $($apk.Name)"
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    foreach ($name in @('codepass-windows.zip', 'codepass-sms-magisk.zip')) {
        $path = Join-Path $releaseDir $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
        $zip = [System.IO.Compression.ZipFile]::OpenRead($path)
        try {
            foreach ($entry in $zip.Entries) {
                if ($entry.FullName -match '\\|(^|/)(config\.ini|config\.conf|lsposed\.conf|history\.txt|[^/]+\.log|[^/]+\.(jks|keystore|p12|pfx))$') {
                    Add-Block "unexpected runtime data, signing material or Windows path separator in $name"
                }
            }
            $required = if ($name -eq 'codepass-windows.zip') { @('codepass.exe','LICENSE','PRIVACY.md','THIRD_PARTY_NOTICES.md') } else { @('module.prop','service.sh','forward.sh','config.example','webroot/index.html') }
            foreach ($entryName in $required) {
                if ($null -eq $zip.GetEntry($entryName)) { Add-Block "missing entry in ${name}: $entryName" }
            }
        } finally { $zip.Dispose() }
    }
    if ($RequireArtifacts) {
        foreach ($name in @('codepass-windows.zip','codepass-sms-magisk.zip','codepass-lsposed-release.apk','SHA256SUMS.txt')) {
            if (-not (Test-Path -LiteralPath (Join-Path $releaseDir $name) -PathType Leaf)) { Add-Block "missing final release artifact: $name" }
        }
        $hashPath = Join-Path $releaseDir 'SHA256SUMS.txt'
        if (Test-Path -LiteralPath $hashPath -PathType Leaf) {
            $hashLines = @(Get-Content -LiteralPath $hashPath)
            foreach ($name in @('codepass-windows.zip','codepass-sms-magisk.zip','codepass-lsposed-release.apk')) {
                $path = Join-Path $releaseDir $name
                if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
                $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
                $matches = @($hashLines | Where-Object { $_ -match ('^([0-9a-fA-F]{64})\s+\*?' + [regex]::Escape($name) + '$') })
                if ($matches.Count -ne 1 -or ([regex]::Match($matches[0], '^[0-9a-fA-F]{64}').Value -ne $hash)) {
                    Add-Block "missing or stale SHA256SUMS entry: $name"
                } else { Add-Pass "SHA256SUMS verified: $name" }
            }
        }
        $signedApk = Join-Path $releaseDir 'codepass-lsposed-release.apk'
        if (Test-Path -LiteralPath $signedApk -PathType Leaf) {
            $sdk = $env:ANDROID_HOME
            if (-not $sdk) { $sdk = [Environment]::GetEnvironmentVariable('ANDROID_HOME','User') }
            $signer = if ($sdk -and (Test-Path -LiteralPath (Join-Path $sdk 'build-tools'))) {
                Get-ChildItem -LiteralPath (Join-Path $sdk 'build-tools') -Directory |
                    Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } | Sort-Object { [version]$_.Name } -Descending |
                    ForEach-Object { Join-Path $_.FullName 'apksigner.bat' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
            }
            if (-not $signer) { Add-Block 'cannot locate apksigner to verify the final APK' }
            else {
                $result = & $signer verify --verbose --print-certs $signedApk 2>&1
                if ($LASTEXITCODE -ne 0 -or ($result -join "`n") -match 'CN=Android Debug(?:,|$)') { Add-Block 'final APK signature failed or uses Android Debug certificate' }
                else { Add-Pass 'final APK signature verified (still confirm the fixed certificate fingerprint)' }
            }
        }
    }
} else {
    if ($RequireArtifacts) { Add-Block 'missing release directory' }
    else { Add-Warn 'no release directory yet; artifacts have not been checked' }
}

# ------------------------------------------------------ 6. .gitignore coverage
$ignorePath = Join-Path $Root '.gitignore'
if (Test-Path -LiteralPath $ignorePath -PathType Leaf) {
    $ignore = Get-Content -LiteralPath $ignorePath -Raw -Encoding UTF8
    $expected = @('release/', 'archive/', '**/config.ini', '**/history.txt', '*.log', '*.keystore', '*.jks', '**/key.properties')
    foreach ($pattern in $expected) {
        if ($ignore -match [regex]::Escape($pattern)) {
            Add-Pass ".gitignore covers $pattern"
        } else {
            Add-Warn ".gitignore may be missing a rule for $pattern"
        }
    }
} else {
    Add-Block 'missing .gitignore'
}

# ------------------------------------------------------ 7. version consistency
$androidBuild = Get-Content -LiteralPath (Join-Path $Root 'phone\lsposed\app\build.gradle') -Raw -Encoding UTF8
$moduleProp = Get-Content -LiteralPath (Join-Path $Root 'phone\magisk\module.prop') -Raw -Encoding UTF8
$updaterSource = Get-Content -LiteralPath (Join-Path $Root 'windows\src\Updater.cs') -Raw -Encoding UTF8
$androidVersion = [regex]::Match($androidBuild, "versionName\s+'([^']+)'").Groups[1].Value
$magiskVersion = [regex]::Match($moduleProp, '(?m)^version=([^\r\n]+)').Groups[1].Value
$windowsVersion = [regex]::Match($updaterSource, 'AppVersion\s*=\s*"([^"]+)"').Groups[1].Value
if ($androidVersion -and $androidVersion -eq $magiskVersion -and $androidVersion -eq $windowsVersion) {
    Add-Pass "all component versions match: $androidVersion"
} else { Add-Block "version mismatch: Android=[$androidVersion] Magisk=[$magiskVersion] Windows=[$windowsVersion]" }

# ------------------------------------------------------------------ 7. report
Write-Host ("PASS  : {0}" -f $script:passed.Count) -ForegroundColor Green
foreach ($m in $script:passed) { Write-Host "  [ok]   $m" -ForegroundColor DarkGreen }

Write-Host ("WARN  : {0}" -f $script:warns.Count) -ForegroundColor Yellow
foreach ($m in $script:warns) { Write-Host "  [warn] $m" -ForegroundColor Yellow }

Write-Host ("BLOCK : {0}" -f $script:blocks.Count) -ForegroundColor Red
foreach ($m in $script:blocks) { Write-Host "  [stop] $m" -ForegroundColor Red }

Write-Host ''
if ($script:blocks.Count -gt 0) {
    Write-Host 'Result: blockers found. Do NOT publish yet.' -ForegroundColor Red
    exit 1
}
Write-Host 'Result: checks passed. Final publication requires -RequireArtifacts plus device testing.' -ForegroundColor Green
exit 0
