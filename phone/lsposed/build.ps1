[CmdletBinding()]
param(
    [switch]$Release,
    [string]$EnvironmentRoot = '',
    [switch]$SignAndroid,
    [string]$AndroidKeystore = '',
    [string]$AndroidKeyAlias = '',
    [string]$AndroidStorePasswordEnv = 'CODEPASS_ANDROID_STORE_PASSWORD',
    [string]$AndroidKeyPasswordEnv = 'CODEPASS_ANDROID_KEY_PASSWORD'
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
# This repository ships only the native Java/LSPosed implementation.
if ($SignAndroid -and -not $Release) { throw '-SignAndroid requires -Release.' }
if ($SignAndroid) {
    if ([string]::IsNullOrWhiteSpace($AndroidKeystore) -or [string]::IsNullOrWhiteSpace($AndroidKeyAlias)) {
        throw '-SignAndroid requires -AndroidKeystore and -AndroidKeyAlias.'
    }
    if (-not (Test-Path -LiteralPath $AndroidKeystore -PathType Leaf)) { throw 'Android keystore file not found.' }
    $storePassword = [Environment]::GetEnvironmentVariable($AndroidStorePasswordEnv)
    $keyPassword = [Environment]::GetEnvironmentVariable($AndroidKeyPasswordEnv)
    if ([string]::IsNullOrEmpty($storePassword) -or [string]::IsNullOrEmpty($keyPassword)) {
        throw "Android signing password environment variables are required: $AndroidStorePasswordEnv and $AndroidKeyPasswordEnv"
    }
}
$gradle = Join-Path $root 'gradlew.bat'
$task = if ($Release) { 'assembleRelease' } else { 'assembleDebug' }

# Prefer the project-local Android environment when it is present. This keeps
# the build independent from machine-wide Java, Gradle, and SDK settings.
$projectRoot = Split-Path -Parent (Split-Path -Parent $root)
$usingPortable = $false
$portableRootWasRequested = $PSBoundParameters.ContainsKey('EnvironmentRoot')
if ([string]::IsNullOrWhiteSpace($EnvironmentRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CODEPASS_ANDROID_ENV)) {
        $EnvironmentRoot = $env:CODEPASS_ANDROID_ENV
    } elseif (-not [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
        $EnvironmentRoot = Split-Path -Parent $env:ANDROID_HOME
    } else {
        $EnvironmentRoot = Join-Path $projectRoot 'android-env'
    }
} elseif (-not [System.IO.Path]::IsPathRooted($EnvironmentRoot)) {
    $EnvironmentRoot = Join-Path (Get-Location).Path $EnvironmentRoot
}
$EnvironmentRoot = [System.IO.Path]::GetFullPath($EnvironmentRoot)

$portableJdk = $null
$portableGradle = $null
$portableSdk = Join-Path $EnvironmentRoot 'sdk'
if (Test-Path (Join-Path $EnvironmentRoot 'jdk')) {
    $portableJdk = Get-ChildItem (Join-Path $EnvironmentRoot 'jdk') -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'bin\java.exe') } |
        Select-Object -First 1
}
if (Test-Path (Join-Path $EnvironmentRoot 'gradle')) {
    $portableGradle = Get-ChildItem (Join-Path $EnvironmentRoot 'gradle') -Directory |
        Where-Object { Test-Path (Join-Path $_.FullName 'bin\gradle.bat') } |
        Select-Object -First 1
}

if ($portableJdk -and $portableGradle -and (Test-Path $portableSdk)) {
    $env:JAVA_HOME = $portableJdk.FullName
    $env:ANDROID_HOME = $portableSdk
    $env:ANDROID_SDK_ROOT = $portableSdk
    $env:GRADLE_USER_HOME = Join-Path $EnvironmentRoot 'gradle-home'
    New-Item -ItemType Directory -Force -Path $env:GRADLE_USER_HOME | Out-Null
    $usingPortable = $true
} elseif ($portableRootWasRequested) {
    throw "Android environment is incomplete: $EnvironmentRoot"
}
if ($SignAndroid -and [string]::IsNullOrWhiteSpace($env:ANDROID_HOME)) {
    throw 'ANDROID_HOME is not set; cannot locate build-tools for signing.'
}

Push-Location $root
try {
    if (Test-Path $gradle) {
        & $gradle --no-daemon $task
    } elseif ($usingPortable) {
        $portableGradleCommand = Join-Path $portableGradle.FullName 'bin\gradle.bat'
        Write-Host "Using portable Android environment: $EnvironmentRoot"
        & $portableGradleCommand --no-daemon -p $root $task
    } else {
        $globalGradle = Get-Command gradle -ErrorAction SilentlyContinue
        if ($null -eq $globalGradle) {
            throw "Gradle not found. Install Gradle or add a Gradle Wrapper in $root."
        }
        & $globalGradle.Source --no-daemon -p $root $task
    }
    if ($LASTEXITCODE -ne 0) { throw "Gradle build failed (exit $LASTEXITCODE)" }
} finally {
    Pop-Location
}

$variant = if ($Release) { 'release' } else { 'debug' }
$apkDir = Join-Path $root "app\build\outputs\apk\$variant"
$apk = Join-Path $apkDir "app-$variant.apk"
if (-not (Test-Path $apk)) {
    $unsigned = Join-Path $apkDir "app-$variant-unsigned.apk"
    if ($Release -and (Test-Path $unsigned)) {
        $apk = $unsigned
        if (-not $SignAndroid) {
            Write-Warning "Release APK is unsigned; sign it before installing."
        }
    } else {
        throw "APK was not produced: $apk"
    }
}

if ($SignAndroid) {
    $releaseDir = Join-Path $root '..\..\release'
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
    $buildToolsRoot = Join-Path $env:ANDROID_HOME 'build-tools'
    $buildTools = if (Test-Path -LiteralPath $buildToolsRoot -PathType Container) {
        Get-ChildItem -LiteralPath $buildToolsRoot -Directory
    } else { @() }
    $buildTools = $buildTools |
        Where-Object { $_.Name -match '^\d+\.\d+\.\d+$' } |
        Sort-Object { [version]$_.Name } -Descending
    $zipalign = $buildTools | ForEach-Object { Join-Path $_.FullName 'zipalign.exe' } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    $signer = $buildTools | ForEach-Object { Join-Path $_.FullName 'apksigner.bat' } |
        Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $zipalign -or -not $signer) { throw 'zipalign/apksigner not found in the Android SDK build-tools.' }

    $aligned = Join-Path $apkDir 'app-release-aligned.apk'
    $signed = Join-Path $apkDir 'app-release-signed.apk'
    & $zipalign -p -f 4 $apk $aligned
    if ($LASTEXITCODE -ne 0) { throw "zipalign failed (exit $LASTEXITCODE)" }
    # Passwords come from environment variables and are never stored in the repo.
    & $signer sign --ks (Resolve-Path -LiteralPath $AndroidKeystore).ProviderPath `
        --ks-key-alias $AndroidKeyAlias `
        --ks-pass "env:$AndroidStorePasswordEnv" --key-pass "env:$AndroidKeyPasswordEnv" `
        --out $signed $aligned
    if ($LASTEXITCODE -ne 0) { throw "apksigner sign failed (exit $LASTEXITCODE)" }
    $verification = & $signer verify --verbose --print-certs $signed 2>&1
    if ($LASTEXITCODE -ne 0) { throw 'APK signature verification failed; no signed APK was copied to release.' }
    if (($verification -join "`n") -match '(?im)^Signer #\d+ certificate DN:.*CN=Android Debug(?:,|$)') {
        throw 'Android debug certificates must not be used for distribution.'
    }
    $out = Join-Path $releaseDir 'codepass-lsposed-release.apk'
    Copy-Item -LiteralPath $signed -Destination $out -Force
    Write-Host "OK -> $out" -ForegroundColor Green
} else {
    Write-Host "Development artifact -> $apk" -ForegroundColor Yellow
    Write-Host 'Only a verified, release-signed APK is copied to release.'
}
