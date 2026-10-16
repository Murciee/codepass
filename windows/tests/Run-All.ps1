# Isolated build + synthetic regression. Windows PowerShell 5.1, ASCII only.
[CmdletBinding()]
param([string]$ExePath = '', [switch]$AllowDifferentSource)
$ErrorActionPreference = 'Stop'
$windowsRoot = Split-Path -Parent $PSScriptRoot
$repo = Split-Path -Parent $windowsRoot
$run = Join-Path $windowsRoot ('build\regression-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $run | Out-Null
$powerShell = Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$results = New-Object 'System.Collections.Generic.List[object]'
$ownedFixtures = New-Object 'System.Collections.Generic.List[string]'
$pass = 0; $fail = 0; $fatal = $null
try {
    if (-not $ExePath) {
        $buildOutput = & $powerShell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $windowsRoot 'build.ps1') -OutputDirectory (Join-Path $run 'bin') -SkipPackage 2>&1
        $buildCode = $LASTEXITCODE
        $buildOutput | Set-Content -LiteralPath (Join-Path $run 'build.log') -Encoding UTF8
        if ($buildCode -ne 0) { throw 'Build failed; see build.log' }
        $ExePath = Join-Path $run 'bin\codepass.exe'
    }
    $ExePath = [IO.Path]::GetFullPath($ExePath)
    $metaPath = Join-Path (Split-Path -Parent $ExePath) 'build-info.json'
    $meta = Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $hash = (Get-FileHash -LiteralPath $ExePath -Algorithm SHA256).Hash
    $versionInfo = [Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
    if ($meta.exeSha256 -ne $hash -or $versionInfo.ProductVersion -ne $meta.buildId -or $versionInfo.FileVersion -ne ($meta.version + '.0')) {
        throw 'EXE does not match build-info.json or version stamp'
    }
    if (-not $AllowDifferentSource) {
        $sourceFiles = @(Get-ChildItem -LiteralPath (Join-Path $windowsRoot 'src') -Filter '*.cs' -File | Sort-Object Name)
        $inputs = @($sourceFiles | ForEach-Object { 'src/' + $_.Name + ':' + (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash })
        $inputs += 'build.ps1:' + (Get-FileHash -LiteralPath (Join-Path $windowsRoot 'build.ps1') -Algorithm SHA256).Hash
        $inputs += 'codepass.ico:' + (Get-FileHash -LiteralPath (Join-Path $windowsRoot 'codepass.ico') -Algorithm SHA256).Hash
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $sourceHash = ([BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes(($inputs -join "`n"))))).Replace('-','').ToLowerInvariant() }
        finally { $sha.Dispose() }
        if ($meta.sourceSha256 -ne $sourceHash) { throw 'EXE source digest differs from current source; rebuild or explicitly use -AllowDifferentSource' }
    }
    Write-Host ('BUILD_ID=' + $meta.buildId)
    Write-Host ('SHA256=' + $hash)
    foreach ($suite in @('test-email-security-code-history.ps1','test-email-switch-sms-limit.ps1','test-privacy-quality.ps1','test-history-diagnostics-layout.ps1','test-startup-retention.ps1','test-receiver-binding.ps1','test-delivery-usability.ps1','test-updater.ps1')) {
        $fixture = Join-Path $run ($suite.Replace('.ps1','') + '-fixtures')
        $ownedFixtures.Add($fixture)
        $suiteArguments = @('-ExePath', $ExePath, '-FixtureDirectory', $fixture)
        if ($suite -eq 'test-email-security-code-history.ps1' -or $suite -eq 'test-history-diagnostics-layout.ps1') { $suiteArguments += @('-EvidenceDirectory', (Join-Path $run 'evidence')) }
        # 子进程写 stderr 时 PowerShell 5.1 会把它包成 NativeCommandError，在 EAP=Stop 下会直接中止整个
        # 运行（表现为 ERROR=... 且 pass=0）。这里临时放宽，让套件失败按它自己的 TOTAL 与退出码记录，
        # 其余套件继续跑完。
        $savedPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $output = @(& $powerShell -NoProfile -STA -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot $suite) @suiteArguments 2>&1)
            $exitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $savedPreference }
        $output | Set-Content -LiteralPath (Join-Path $run ($suite + '.log')) -Encoding UTF8
        $totals = @($output | ForEach-Object { [regex]::Match([string]$_, '^TOTAL pass=(\d+) fail=(\d+)$') } | Where-Object { $_.Success })
        $suitePass = 0; $suiteFail = 0
        if ($totals.Count -eq 1) { $suitePass = [int]$totals[0].Groups[1].Value; $suiteFail = [int]$totals[0].Groups[2].Value }
        if ($exitCode -ne 0 -or $totals.Count -ne 1 -or $suitePass -eq 0) { $suiteFail = [Math]::Max(1,$suiteFail) }
        $pass += $suitePass; $fail += $suiteFail
        $evidenceUnavailable = @($output | Where-Object { [string]$_ -like 'EVIDENCE_UNAVAILABLE=*' }).Count
        $results.Add([ordered]@{ suite = $suite; pass = $suitePass; fail = $suiteFail; exitCode = $exitCode; evidenceUnavailable = $evidenceUnavailable })
        if ($evidenceUnavailable -gt 0) { Write-Host ('WARNING: ' + $suite + ' screenshot evidence unavailable=' + $evidenceUnavailable) }
        Write-Host ($suite + ': pass=' + $suitePass + ' fail=' + $suiteFail + ' exit=' + $exitCode)
    }
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $diffOutput = @(& git -C $repo -c core.safecrlf=false diff --check -- windows/src windows/tests windows/build.ps1 README.md README.en.md CHANGELOG.md docs/windows-regression.md docs/RELEASE_CHECKLIST.md 2>&1)
        $diffCode = $LASTEXITCODE
    } finally { $ErrorActionPreference = $savedPreference }
    if ($diffCode -ne 0) { $fail++; $diffOutput | Set-Content -LiteralPath (Join-Path $run 'diff-check.log') -Encoding UTF8 }
} catch {
    $fatal = $_.Exception.Message; $fail++
    Write-Host ('ERROR=' + $fatal)
} finally {
    foreach ($fixture in $ownedFixtures) {
        if (-not (Test-Path -LiteralPath $fixture)) { continue }
        try {
            if ((Get-Item -LiteralPath $fixture).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing linked fixture' }
            if (Get-ChildItem -LiteralPath $fixture -Recurse -Force | Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint }) { throw 'Refusing contained link' }
            # 回收放到独立的 STA 子进程里执行：在长驻的 runner 进程里，Shell.Application.MoveHere 会在
            # 最后一个 fixture 上阻塞，VisualBasic 回收接口则报 "系统调用级别不正确"；同样的调用在
            # 新进程里稳定成功。语义不变（仍然移到回收站），返回后确认目录已消失。
            $recycleCommand = "Add-Type -AssemblyName Microsoft.VisualBasic; [Microsoft.VisualBasic.FileIO.FileSystem]::DeleteDirectory('" + ($fixture.Replace("'", "''")) + "', [Microsoft.VisualBasic.FileIO.UIOption]::OnlyErrorDialogs, [Microsoft.VisualBasic.FileIO.RecycleOption]::SendToRecycleBin)"
            & $powerShell -NoProfile -STA -ExecutionPolicy Bypass -Command $recycleCommand 2>&1 | Out-Null
            if (Test-Path -LiteralPath $fixture) { throw 'Recycling not confirmed' }
        } catch { $fail++; Write-Host ('CLEANUP_ERROR=' + $_.Exception.Message) }
    }
    $report = [ordered]@{ buildId = $(if ($meta) { $meta.buildId }); exeSha256 = $hash; exePath = $ExePath; suites = @($results.ToArray()); pass = $pass; fail = $fail; error = $fatal }
    $report | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'summary.json') -Encoding UTF8
}
Write-Host ('REPORT=' + (Join-Path $run 'summary.json'))
Write-Host ('TOTAL pass=' + $pass + ' fail=' + $fail)
if ($fail -gt 0) { exit 1 }
