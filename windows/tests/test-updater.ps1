# Synthetic release packages only. Never install into the actual app directory.
param([string]$ExePath = (Join-Path $PSScriptRoot '..\dist\codepass.exe'), [string]$FixtureDirectory = '')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$updater=$asm.GetType('Codepass.Updater')
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$root=if($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $PSScriptRoot ('..\build\updater-fixtures-'+[Guid]::NewGuid().ToString('N')) }
[void](New-Item -ItemType Directory -Force -Path $root)
$pass=0; $fail=0
function Assert([string]$name,[bool]$ok) { if($ok) { $script:pass++; 'PASS '+$name } else { $script:fail++; 'FAIL '+$name } }
function Call([string]$name,[object[]]$values) {
    $parameters=New-Object object[] $values.Length
    for($i=0;$i -lt $values.Length;$i++) { if($null -ne $values[$i]) { $parameters[$i]=$values[$i].PSObject.BaseObject } }
    return ,($updater.GetMethod($name,$flags).Invoke($null,$parameters))
}
function Rejected([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$newDir=Join-Path $root 'new'; $oldDir=Join-Path $root 'old'
[void](New-Item -ItemType Directory -Path $newDir,$oldDir)
foreach($item in @(@($newDir,'9.8.7'),@($oldDir,'1.0.0'))) {
    $source=Join-Path $item[0] 'fixture.cs'
    $code='using System; using System.IO; using System.Reflection; [assembly:AssemblyVersion("'+$item[1]+'.0")] class Fixture { static void Main(string[] args) { if(args.Length > 0 && args[0] == "--wait") { System.Threading.Thread.Sleep(1500); return; } string root=AppDomain.CurrentDomain.BaseDirectory; if(File.Exists(Path.Combine(root,"fail-startup"))) return; File.WriteAllText(Path.Combine(root,"restarted.txt"),"'+$item[1]+'"); if(args.Length == 2 && args[0] == "--updated") { File.WriteAllText(Path.Combine(root,".codepass-update",args[1],"ready-"+System.Diagnostics.Process.GetCurrentProcess().Id),"ready"); System.Threading.Thread.Sleep(3000); } } }'
    [IO.File]::WriteAllText($source,$code,[Text.Encoding]::ASCII)
    $outputArgument='/out:'+(Join-Path $item[0] 'codepass.exe')
    & $csc /nologo /target:winexe $outputArgument $source
    if($LASTEXITCODE -ne 0) { throw 'Synthetic fixture build failed' }
}
$newExe=Join-Path $newDir 'codepass.exe'; $oldExe=Join-Path $oldDir 'codepass.exe'
$newHash=(Get-FileHash -LiteralPath $newExe).Hash
$oldHash=(Get-FileHash -LiteralPath $oldExe).Hash
function MakeCase([string]$name,[string]$mode='good') {
    $directory=Join-Path $root $name
    $stage=Join-Path $directory ('.codepass-update\'+[Guid]::NewGuid().ToString('N'))
    [void](New-Item -ItemType Directory -Force -Path $stage)
    $target=Join-Path $directory 'codepass.exe'
    Copy-Item -LiteralPath $oldExe -Destination $target
    [IO.File]::WriteAllText((Join-Path $directory 'config.ini'),'fixture-config')
    [IO.File]::WriteAllText((Join-Path $directory 'history.txt'),'fixture-history')
    $package=Join-Path $stage 'package.zip'
    $stream=[IO.File]::Create($package)
    $zip=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create)
    try {
        $info=@{version='9.8.7';exeSha256=$newHash}
        if($mode -eq 'hash') { $info.exeSha256=('0'*64) }
        if($mode -eq 'version') { $info.version='9.8.6' }
        $entry=$zip.CreateEntry('build-info.json'); $writer=New-Object IO.StreamWriter($entry.Open(),[Text.Encoding]::UTF8)
        try { $writer.Write(($info | ConvertTo-Json -Compress)) } finally { $writer.Dispose() }
        if($mode -ne 'missing') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$newExe,'codepass.exe') }
        if($mode -eq 'duplicate') { [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$newExe,'CODEPASS.EXE') }
        if($mode -eq 'traversal') { [void]$zip.CreateEntry('../config.ini') }
    } finally { $zip.Dispose(); $stream.Dispose() }
    $releaseJson=@{tag_name='v9.8.7';draft=$false;prerelease=$false;assets=@(@{name='codepass-windows.zip';size=(Get-Item $package).Length;browser_download_url='https://github.com/Murciee/codepass/releases/download/v9.8.7/codepass-windows.zip';digest=('sha256:'+(Get-FileHash $package).Hash)})} | ConvertTo-Json -Depth 4 -Compress
    $release=Call 'ParseRelease' @($releaseJson)
    return @{Directory=$directory;Stage=$stage;Target=$target;Package=$package;Release=$release;Json=$releaseJson}
}
$case=MakeCase 'success'
Assert 'update:newer-version' (Call 'IsNewer' @('v9.8.7','1.0.6'))
Assert 'update:same-version' (-not (Call 'IsNewer' @('v1.0.6','1.0.6')))
Assert 'update:reject-invalid-version' (Rejected { Call 'ParseRelease' @($case.Json.Replace('v9.8.7','v9.8.7-beta')) })
Assert 'update:reject-prerelease' (Rejected { Call 'ParseRelease' @($case.Json.Replace('"prerelease":false','"prerelease":true')) })
$plan=Call 'PreparePackage' @($case.Release,$case.Package,$case.Target,$case.Stage)
Assert 'update:stage-hash' ((Get-FileHash (Join-Path $case.Stage 'codepass.exe')).Hash -eq $newHash)
Assert 'update:old-untouched-before-install' ((Get-FileHash $case.Target).Hash -eq $oldHash)
$backup=Call 'InstallFiles' @($plan)
Assert 'update:installed-exe' ((Get-FileHash $case.Target).Hash -eq $newHash)
Assert 'update:backup-old-exe' ((Get-FileHash $backup).Hash -eq $oldHash)
Assert 'update:config-preserved' ([IO.File]::ReadAllText((Join-Path $case.Directory 'config.ini')) -ceq 'fixture-config')
Assert 'update:history-preserved' ([IO.File]::ReadAllText((Join-Path $case.Directory 'history.txt')) -ceq 'fixture-history')
[void](Call 'RestoreOriginal' @($plan))
Assert 'update:rollback-restores-original' ((Get-FileHash $case.Target).Hash -eq $oldHash)
Assert 'update:rollback-keeps-backup' ((Get-FileHash $backup).Hash -eq $oldHash)
$partial=MakeCase 'partial replace'
$partialPlan=Call 'PreparePackage' @($partial.Release,$partial.Package,$partial.Target,$partial.Stage)
$partialBackup=Join-Path $partial.Stage 'original.exe'
[IO.File]::Move($partial.Target,$partialBackup)
$partialPlan.GetType().GetField('BackupPath',$flags).SetValue($partialPlan,[string]$partialBackup)
[void](Call 'RestoreOriginal' @($partialPlan))
Assert 'update:partial-replace-restores-missing-target' ((Get-FileHash $partial.Target).Hash -eq $oldHash)
foreach($mode in @('hash','version','missing','duplicate','traversal')) {
    $bad=MakeCase $mode $mode
    Assert ('update:reject-'+$mode) (Rejected { Call 'PreparePackage' @($bad.Release,$bad.Package,$bad.Target,$bad.Stage) })
    Assert ('update:reject-preserves-old-'+$mode) ((Get-FileHash $bad.Target).Hash -eq $oldHash)
}
$bad=MakeCase 'digest'
$bad.Release.Assets[0].Digest='sha256:'+('0'*64)
Assert 'update:reject-package-digest' (Rejected { Call 'PreparePackage' @($bad.Release,$bad.Package,$bad.Target,$bad.Stage) })
$bad.Release.Assets[0].Url='https://example.invalid/codepass-windows.zip'
Assert 'update:reject-foreign-download' (Rejected { Call 'SelectAsset' @($bad.Release) })
$bad=MakeCase 'tampered'
$plan=Call 'PreparePackage' @($bad.Release,$bad.Package,$bad.Target,$bad.Stage)
[IO.File]::AppendAllText($bad.Target,'changed')
$changed=(Get-FileHash $bad.Target).Hash
Assert 'update:reject-changed-target' (Rejected { Call 'InstallFiles' @($plan) })
Assert 'update:changed-target-preserved' ((Get-FileHash $bad.Target).Hash -eq $changed)
$bad=MakeCase 'locked'
$plan=Call 'PreparePackage' @($bad.Release,$bad.Package,$bad.Target,$bad.Stage)
$lock=[IO.File]::Open($bad.Target,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try { Assert 'update:locked-target-rejected' (Rejected { Call 'InstallFiles' @($plan) }) } finally { $lock.Dispose() }
Assert 'update:locked-target-preserved' ((Get-FileHash $bad.Target).Hash -eq $oldHash)
$live=MakeCase 'helper lifecycle'
$plan=Call 'PreparePackage' @($live.Release,$live.Package,$live.Target,$live.Stage)
$parent=Start-Process -FilePath $live.Target -ArgumentList '--wait' -PassThru
$plan.ParentId=$parent.Id
$plan.ParentStartedUtcTicks=$parent.StartTime.ToUniversalTime().Ticks
$planPath=Join-Path $live.Stage 'plan.json'
$json=@{TargetPath=$plan.TargetPath;StageDirectory=$plan.StageDirectory;Version=$plan.Version;ExeSha256=$plan.ExeSha256;OriginalSha256=$plan.OriginalSha256;ParentId=$plan.ParentId;ParentStartedUtcTicks=$plan.ParentStartedUtcTicks} | ConvertTo-Json -Compress
[IO.File]::WriteAllText($planPath,$json,[Text.Encoding]::UTF8)
$helper=Join-Path $live.Stage 'update-helper.exe'
Copy-Item -LiteralPath $ExePath -Destination $helper
$installer=Start-Process -FilePath $helper -ArgumentList '--apply-update' -WorkingDirectory $live.Stage -PassThru
Assert 'update:helper-exits' ($installer.WaitForExit(15000))
Assert 'update:helper-installs-exe' ((Get-FileHash $live.Target).Hash -eq $newHash)
$restartMarker=Join-Path $live.Directory 'restarted.txt'
$watch=[Diagnostics.Stopwatch]::StartNew()
while(-not (Test-Path -LiteralPath $restartMarker) -and $watch.ElapsedMilliseconds -lt 3000) { Start-Sleep -Milliseconds 50 }
Assert 'update:helper-restarts-new-version' ((Test-Path -LiteralPath $restartMarker) -and [IO.File]::ReadAllText($restartMarker) -ceq '9.8.7')
Assert 'update:helper-preserves-config' ([IO.File]::ReadAllText((Join-Path $live.Directory 'config.ini')) -ceq 'fixture-config')
$readyMarker=Join-Path $live.Stage 'complete'
Assert 'update:helper-confirms-startup' (Test-Path -LiteralPath $readyMarker)
$parent.Dispose(); $installer.Dispose()
$failed=MakeCase 'startup failure'
$failedPlan=Call 'PreparePackage' @($failed.Release,$failed.Package,$failed.Target,$failed.Stage)
[void](Call 'InstallFiles' @($failedPlan))
[IO.File]::WriteAllText((Join-Path $failed.Directory 'fail-startup'),'fixture')
Assert 'update:detect-startup-failure' (Rejected { Call 'Restart' @($failed.Target,$failed.Stage,$true) })
[void](Call 'RestoreOriginal' @($failedPlan))
Assert 'update:startup-failure-restores-old' ((Get-FileHash $failed.Target).Hash -eq $oldHash)
'TOTAL pass='+$pass+' fail='+$fail
if($fail -gt 0) { exit 1 }
