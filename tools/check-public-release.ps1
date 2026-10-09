# Read-only pre-publish checks for codepass.
# Does not use the network, does not modify anything, and never prints secret values.
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-public-release.ps1
# Exit code: 0 = no blockers, 1 = blockers found.
# NOTE: keep this file ASCII-only (Windows PowerShell mis-decodes UTF-8 without BOM).

[CmdletBinding()]
param(
    [string]$Root = ''
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
    'flutter-env', 'android-env', 'release', 'gradle-home', 'pub-cache'
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
    if ($apks.Count -eq 0) { Add-Pass 'no APK staged in release' }
    foreach ($apk in $apks) {
        if ($apk.Name -match 'debug|unsigned') {
            Add-Warn "APK must not be published as a normal install package: $($apk.Name)"
        } else {
            Add-Warn "confirm the release APK is signed with the fixed certificate: $($apk.Name)"
        }
    }

    $latestFile = Join-Path $releaseDir 'codepass-windows-flutter-latest.txt'
    if (Test-Path -LiteralPath $latestFile -PathType Leaf) {
        $latest = (Get-Content -LiteralPath $latestFile -Raw).Trim()
        $bundle = Join-Path $releaseDir $latest
        if (Test-Path -LiteralPath $bundle -PathType Container) {
            $signed = $true
            foreach ($exe in @('codepass.exe', 'codepass-service.exe', 'codepass-ui.exe')) {
                $exePath = Join-Path $bundle $exe
                if (-not (Test-Path -LiteralPath $exePath -PathType Leaf)) {
                    Add-Warn "Windows bundle is missing $exe"
                    continue
                }
                $sig = Get-AuthenticodeSignature -LiteralPath $exePath
                if ($sig.Status -ne 'Valid') { $signed = $false }
            }
            if ($signed) {
                Add-Pass 'Windows executables have a valid Authenticode signature'
            } else {
                Add-Warn 'Windows executables are NOT code-signed; label the download accordingly'
            }
        }
    }
} else {
    Add-Pass 'no release directory yet'
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
Write-Host 'Result: no blockers found. Review the warnings, then publish.' -ForegroundColor Green
exit 0
