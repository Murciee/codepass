$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$release = Join-Path $root '..\..\release'
$out = Join-Path $release 'codepass-sms-magisk.zip'

# Explicit allowlist: only these entries are packaged, so local runtime data or
# signing material cannot leak into the published module by accident.
$allow = @('module.prop', 'service.sh', 'action.sh', 'forward.sh', 'README.md', 'config.example', 'webroot')

# Refuse to build if local runtime data is present, even though git ignores it.
$forbidden = @('config.conf', 'codepass.log', 'lsposed.conf', 'config.ini', 'history.txt')
foreach ($name in $forbidden) {
    if (Test-Path -LiteralPath (Join-Path $root $name)) {
        throw "Refusing to package: remove local runtime file '$name' from $root before building."
    }
}

foreach ($name in $allow) {
    $path = Join-Path $root $name
    if (-not (Test-Path -LiteralPath $path)) { throw "Module entry not found: $name" }
}

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

New-Item -ItemType Directory -Force -Path $release | Out-Null

# Write ZIP entries explicitly with '/' separators: Compress-Archive under
# Windows PowerShell 5.1 stores '\' paths, which unzip on Android treats as
# literal filename characters instead of directories (breaking webroot/).
# Build into a temporary file first: a failed build must not replace the last
# known-good release zip with a half-written archive.
$tmp = "$out.tmp"
$stream = [System.IO.File]::Open($tmp, [System.IO.FileMode]::Create, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
try {
    $zip = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($name in $allow) {
            $path = Join-Path $root $name
            if (Test-Path -LiteralPath $path -PathType Container) {
                Get-ChildItem -LiteralPath $path -Recurse -File | ForEach-Object {
                    $relative = $_.FullName.Substring($path.Length + 1).Replace('\', '/')
                    [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $_.FullName, "$name/$relative") | Out-Null
                }
            } else {
                [System.IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $path, $name) | Out-Null
            }
        }
    } finally {
        $zip.Dispose()
    }
} finally {
    $stream.Dispose()
}
Move-Item -LiteralPath $tmp -Destination $out -Force
Write-Host "OK -> $out" -ForegroundColor Green
Write-Host "Packaged entries: $($allow -join ', ')"
Write-Host "Remember to bump version/versionCode in module.prop before publishing." -ForegroundColor Yellow
