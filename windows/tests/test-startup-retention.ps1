# Synthetic fixtures only; never changes the user's startup registry entry.
param([string]$ExePath,[string]$FixtureDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath)); $flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$cfgType=$asm.GetType('Codepass.Config'); $program=$asm.GetType('Codepass.Program'); $history=$asm.GetType('Codepass.History')
$clipboard=$program.GetField('Clipboard',$flags).GetValue($null); $clipboardType=$clipboard.GetType()
$receipts=$history.GetField('Receipts',$flags).GetValue($null)
$itemType=$asm.GetType('Codepass.HistoryItem'); $windowType=$asm.GetType('Codepass.MainWindow')
$root=[IO.Path]::GetFullPath($FixtureDirectory); New-Item -ItemType Directory -Path $root | Out-Null
$pass=0; $fail=0
function Assert([string]$name,[bool]$ok) { if($ok){$script:pass++; Write-Output ('PASS '+$name)}else{$script:fail++;Write-Output ('FAIL '+$name)} }
function Call($type,[string]$name,$target,[object[]]$values) {
 $args=New-Object object[] $values.Length
 for($i=0;$i -lt $values.Length;$i++){if($null -ne $values[$i]){$args[$i]=$values[$i].PSObject.BaseObject}}
 if($null -ne $target){$target=$target.PSObject.BaseObject}
 $method=$type.GetMethods($flags)|Where-Object{$_.Name -eq $name -and $_.GetParameters().Length -eq $values.Length}|Select-Object -First 1
 return ,($method.Invoke($target,$args))
}
function Rejected([scriptblock]$action){try{& $action|Out-Null;return $false}catch{return $true}}
function AddItem([string]$channel,[DateTime]$received,[string]$code,[string]$key='',[string]$mailbox='',[long]$uid=0) {
 $item=[Activator]::CreateInstance($itemType,$true);$item.Channel=$channel;$item.Time=$received.ToLocalTime().ToString('MM-dd HH:mm:ss')
 $item.ReceivedUtcTicks=$received.Ticks;$item.Code=$code;$item.MessageKey=$key;$item.Source='Synthetic';$item.Raw='Synthetic only'
 $item.MailboxKey=$mailbox;$item.MailUid=$uid
 [void](Call $history 'AddUnique' $null @($item)); return ,$item
}
$cfg=[Activator]::CreateInstance($cfgType,$true);$program.GetField('cfg',$flags).SetValue($null,$cfg)
$cfg.PcIp='192.0.2.50';$cfgPath=Join-Path $root 'config.ini';$cfg.Save($cfgPath)
Assert 'retention:default-ten-minutes' ($cfg.HistoryRetentionMinutes -eq 10)
$cfg.HistoryRetentionMinutes=0;$cfg.Save($cfgPath)
Assert 'retention:disabled-roundtrip' ((Call $cfgType 'Load' $null @($cfgPath,$true)).HistoryRetentionMinutes -eq 0)
$legacy=Join-Path $root 'legacy.ini';[IO.File]::WriteAllText($legacy,"port=8787`nmin_len=4`nmax_len=8`n")
Assert 'retention:legacy-config-default-ten' ((Call $cfgType 'Load' $null @($legacy,$true)).HistoryRetentionMinutes -eq 10)
[IO.File]::WriteAllText($legacy,"port=8787`nmin_len=4`nmax_len=8`nhistory_retention_minutes=-1`n")
Assert 'retention:invalid-import-rejected' (Rejected {Call $cfgType 'Load' $null @($legacy,$true)})
Assert 'retention:invalid-runtime-config-falls-back' ((Call $cfgType 'Load' $null @($legacy,$false)).HistoryRetentionMinutes -eq 10)
$cfg.HistoryRetentionMinutes=10;$cfg.Save($cfgPath)
$now=[DateTime]::UtcNow;$historyPath=Join-Path $root 'history.txt';[void](Call $history 'Init' $null @($historyPath))
$old=AddItem 'sms' $now.AddMinutes(-11) '100001'
$oldMail=AddItem 'email' $now.AddMinutes(-12) '100002' 'retention-mail-key'
$boundary=AddItem 'sms' $now.AddMinutes(-10) '100003'
$fresh=AddItem 'email' $now.AddMinutes(-1) '100004'
[void](Call $program 'SetLatestRecord' $null @($old))
Assert 'retention:disabled-does-not-prune' ((Call $history 'PruneExpired' $null @(0,$now)) -eq 0)
Assert 'retention:sms-and-mail-expire' ((Call $history 'PruneExpired' $null @(10,$now)) -eq 2)
Assert 'retention:exact-boundary-kept' ((Call $history 'Snapshot' $null @()).Count -eq 2)
Assert 'retention:expired-tray-code-replaced' ((Call $program 'GetLatestReceipt' $null @()).Code -eq '100004')
Assert 'retention:mail-dedup-survives-pruning' (-not (Call $history 'AddUnique' $null @($oldMail)))
[void](Call $history 'Init' $null @($historyPath))
Assert 'retention:timestamps-survive-restart' ((Call $history 'Snapshot' $null @())[0].ReceivedUtcTicks -eq $fresh.ReceivedUtcTicks)
Assert 'retention:offline-expiry-on-next-start' ((Call $history 'PruneExpired' $null @(10,$now.AddMinutes(20))) -eq 2)
Assert 'retention:expired-last-code-cleared' ($null -eq (Call $program 'GetLatestReceipt' $null @()))
$old=AddItem 'sms' $now.AddMinutes(-11) '100005'
[void](Call $program 'SetLatestRecord' $null @($old))
[IO.File]::SetAttributes($historyPath,[IO.FileAttributes]::ReadOnly)
try{Assert 'retention:failed-save-rejected' (Rejected{Call $history 'PruneExpired' $null @(10,$now)});Assert 'retention:failed-save-keeps-records' ((Call $history 'Snapshot' $null @()).Count -eq 1);Assert 'retention:failed-save-cannot-copy-expired' ($null -eq (Call $program 'GetLatestReceipt' $null @()))}finally{[IO.File]::SetAttributes($historyPath,[IO.FileAttributes]::Normal)}
Assert 'retention:retry-after-save-repair' ((Call $history 'PruneExpired' $null @(10,$now)) -eq 1)
$leap=Call $history 'LegacyReceivedUtc' $null @('02-29 12:00:00',[DateTime]::SpecifyKind([DateTime]'2026-10-11 12:00:00',[DateTimeKind]::Utc))
Assert 'retention:legacy-leap-day-remains-old' ($leap.ToLocalTime().Year -eq 2024)
$crossYear=Call $history 'LegacyReceivedUtc' $null @('12-31 23:00:00',[DateTime]::SpecifyKind([DateTime]'2026-01-02 12:00:00',[DateTimeKind]::Utc))
Assert 'retention:legacy-year-rollover' ($crossYear.ToLocalTime().Year -eq 2025)
$removePath=Join-Path $root 'remove-history.txt';[void](Call $history 'Init' $null @($removePath))
$older=AddItem 'sms' $now.AddMinutes(-2) '224466' 'remove-sms-older'
$twin=AddItem 'email' $now '445566' 'remove-mail-twin'
$current=AddItem 'email' $now '445566' 'remove-mail-current' 'remove-mailbox' 42
[void](Call $program 'SetLatestRecord' $null @($current))
$latest=Call $program 'GetLatestReceipt' $null @()
Assert 'remove:same-code-only-selected-record' ((Call $history 'Remove' $null @($twin)) -and (Call $history 'Snapshot' $null @()).Count -eq 2 -and [object]::ReferenceEquals((Call $history 'Snapshot' $null @())[0],$current))
Assert 'remove:same-code-keeps-unrelated-latest' ([object]::ReferenceEquals($latest,(Call $program 'GetLatestReceipt' $null @())))
Assert 'remove:stale-action-is-no-op' (-not (Call $history 'Remove' $null @($twin)) -and (Call $history 'Snapshot' $null @()).Count -eq 2)
[IO.File]::SetAttributes($removePath,[IO.FileAttributes]::ReadOnly)
try {
 Assert 'remove:failed-save-rejected' (Rejected{Call $history 'Remove' $null @($current)})
 Assert 'remove:failed-save-keeps-order' ((Call $history 'Snapshot' $null @()).Count -eq 2 -and [object]::ReferenceEquals((Call $history 'Snapshot' $null @())[0],$current))
 Assert 'remove:failed-save-keeps-latest' ([object]::ReferenceEquals($latest,(Call $program 'GetLatestReceipt' $null @())))
} finally { [IO.File]::SetAttributes($removePath,[IO.FileAttributes]::Normal) }
Assert 'remove:retry-after-storage-repair' (Call $history 'Remove' $null @($current))
Assert 'remove:latest-falls-back-to-remaining' ((Call $program 'GetLatestReceipt' $null @()).Code -eq '224466')
[void](Call $history 'Init' $null @($removePath))
Assert 'remove:deletion-survives-restart' ((Call $history 'Snapshot' $null @()).Count -eq 1 -and (Call $history 'Snapshot' $null @())[0].Code -eq '224466')
Assert 'remove:mail-dedup-survives-restart' (-not (Call $history 'AddUnique' $null @($current)) -and (Call $history 'Snapshot' $null @()).Count -eq 1)
$pending=$receipts.GetType().GetField('PendingReceipts',$flags).GetValue($receipts)
Assert 'remove:pending-mail-ack-retained' $pending.ContainsKey('remove-mail-current')
[IO.File]::SetAttributes($removePath,[IO.FileAttributes]::ReadOnly)
try { [void](Call $history 'AcknowledgeMailbox' $null @('remove-mailbox',[long]42)) }
finally { [IO.File]::SetAttributes($removePath,[IO.FileAttributes]::Normal) }
Assert 'receipts:failed-ack-keeps-pending-memory' $pending.ContainsKey('remove-mail-current')
[void](Call $history 'Init' $null @($removePath))
Assert 'receipts:failed-ack-keeps-pending-disk' $pending.ContainsKey('remove-mail-current')
[void](Call $history 'AcknowledgeMailbox' $null @('remove-mailbox',[long]42))
Assert 'remove:pending-mail-can-still-be-acknowledged' (-not $pending.ContainsKey('remove-mail-current'))
[void](Call $history 'Init' $null @($removePath))
Assert 'receipts:successful-ack-persists' (-not $pending.ContainsKey('remove-mail-current'))
$historyOnly=[Activator]::CreateInstance($itemType,$true);$historyOnly.Channel='email';$historyOnly.Code='778899';$historyOnly.Time=$now.ToLocalTime().ToString('MM-dd HH:mm:ss');$historyOnly.ReceivedUtcTicks=$now.Ticks;$historyOnly.CanBeLatest=$false
[void](Call $history 'AddUnique' $null @($historyOnly))
[void](Call $program 'RestoreLatestFromHistory' $null @())
$last=(Call $history 'Snapshot' $null @())[1]
Assert 'remove:last-copyable-clears-latest' ((Call $history 'Remove' $null @($last)) -and $null -eq (Call $program 'GetLatestReceipt' $null @()))
Assert 'remove:history-only-record-kept' ((Call $history 'Snapshot' $null @()).Count -eq 1 -and -not (Call $history 'Snapshot' $null @())[0].CanBeLatest)
foreach($prefix in @('r3','d3')) {
 $damagedPath=Join-Path $root ($prefix+'-damaged-ledger.txt')
 $damaged=[IO.File]::ReadAllText($removePath)+$prefix+"`tinvalid-synthetic-payload`n"
 [IO.File]::WriteAllText($damagedPath,$damaged)
 [void](Call $history 'Init' $null @($damagedPath))
 Assert ('receipts:'+ $prefix +'-damaged-ledger-blocks-save') (Rejected { Call $history 'RetrySave' $null @() })
 Assert ('receipts:'+ $prefix +'-damaged-original-retained') ([IO.File]::ReadAllText($damagedPath) -eq $damaged)
}
[void](Call $history 'Init' $null @($removePath))
Assert 'startup:manual-not-silent' (-not (Call $program 'IsSilentStartup' $null @(,[string[]]@('--dry'))))
Assert 'startup:argument-case-insensitive' (Call $program 'IsSilentStartup' $null @(,[string[]]@('--STARTUP')))
Assert 'startup:quoted-command-with-silent-flag' ((Call $program 'StartupCommand' $null @('D:\Synthetic Folder\codepass.exe')) -eq '"D:\Synthetic Folder\codepass.exe" --startup')

# ---- Restart from the tray: argument handling, wait behaviour and menu wiring ----
# Expected menu label for the restart entry, built from code points so this suite stays ASCII-only.
$restartLabel=[string]::Join('',[char[]]@(0x91CD,0x542F,0x5E94,0x7528))
Assert 'restart:wait-pid-parsed' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@('--restart-wait','4242','638000000000000000'))) -eq 4242)
Assert 'restart:wait-flag-case-insensitive' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@('--RESTART-WAIT','77','0'))) -eq 77)
Assert 'restart:wait-pid-with-other-args' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@('--dry','--restart-wait','99','123'))) -eq 99)
Assert 'restart:wait-missing-value' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@('--restart-wait'))) -eq 0)
Assert 'restart:wait-invalid-pid' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@('--restart-wait','abc','1'))) -eq 0)
$selfPid=[string][System.Diagnostics.Process]::GetCurrentProcess().Id
Assert 'restart:wait-self-pid-ignored' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@('--restart-wait',$selfPid,'1'))) -eq 0)
$visibleArgs=[string](Call $program 'RestartArguments' $null @(4242,[long]638123456789012345,$true))
Assert 'restart:arguments-visible' ($visibleArgs -eq '--restart-wait 4242 638123456789012345')
Assert 'restart:arguments-hidden-stays-silent' ((Call $program 'RestartArguments' $null @(4242,[long]0,$false)) -eq '--restart-wait 4242 0 --startup')
Assert 'restart:arguments-round-trip' ((Call $program 'RestartWaitProcessId' $null @(,[string[]]@($visibleArgs -split ' '))) -eq 4242)
# Only update and restart handovers can inherit the old instance's listening socket.
Assert 'handover:update-spawn-detected' (Call $program 'WasSpawnedForHandover' $null @(,[string[]]@('--updated','20261016-1-abcd')))
Assert 'handover:restart-spawn-detected' (Call $program 'WasSpawnedForHandover' $null @(,[string[]]@('--restart-wait','4242','638000000000000000')))
Assert 'handover:manual-launch-not-detected' (-not (Call $program 'WasSpawnedForHandover' $null @(,[string[]]@('--startup'))))
Assert 'handover:plain-launch-not-detected' (-not (Call $program 'WasSpawnedForHandover' $null @(,[string[]]@('--dry'))))
Assert 'handover:null-args-not-detected' (-not (Call $program 'WasSpawnedForHandover' $null @($null)))
$helperExe=Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe'
$helper=Start-Process -FilePath $helperExe -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 2' -PassThru
Start-Sleep -Milliseconds 300
$helperTicks=[string][System.Diagnostics.Process]::GetProcessById($helper.Id).StartTime.ToUniversalTime().Ticks
$watch=[System.Diagnostics.Stopwatch]::StartNew()
[void](Call $program 'WaitForRestartTarget' $null @(,[string[]]@('--restart-wait',[string]$helper.Id,$helperTicks)))
$watch.Stop()
Assert 'restart:waits-until-target-exits' ($watch.Elapsed.TotalSeconds -ge 1.0 -and $watch.Elapsed.TotalSeconds -lt 15)
$live=Start-Process -FilePath $helperExe -ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 5' -PassThru
$watch=[System.Diagnostics.Stopwatch]::StartNew()
[void](Call $program 'WaitForRestartTarget' $null @(,[string[]]@('--restart-wait',[string]$live.Id,'1')))
$watch.Stop()
Assert 'restart:recycled-pid-not-awaited' ($watch.Elapsed.TotalSeconds -lt 1.5)
# Without usable ticks the wait must be skipped (the mutex decides), never an unconditional 30 s stall.
$watch=[System.Diagnostics.Stopwatch]::StartNew()
[void](Call $program 'WaitForRestartTarget' $null @(,[string[]]@('--restart-wait',[string]$live.Id)))
$watch.Stop()
Assert 'restart:missing-ticks-does-not-wait' ($watch.Elapsed.TotalSeconds -lt 1.5)
$watch=[System.Diagnostics.Stopwatch]::StartNew()
[void](Call $program 'WaitForRestartTarget' $null @(,[string[]]@('--restart-wait',[string]$live.Id,'not-a-number')))
$watch.Stop()
Assert 'restart:invalid-ticks-does-not-wait' ($watch.Elapsed.TotalSeconds -lt 1.5)
if(-not $live.HasExited){Stop-Process -Id $live.Id -Force}
$gone=Start-Process -FilePath $helperExe -ArgumentList '-NoProfile','-Command','Start-Sleep -Milliseconds 1200' -PassThru
$goneTicks=[string]$gone.StartTime.ToUniversalTime().Ticks
$gone.WaitForExit()
$watch=[System.Diagnostics.Stopwatch]::StartNew()
[void](Call $program 'WaitForRestartTarget' $null @(,[string[]]@('--restart-wait',[string]$gone.Id,$goneTicks)))
$watch.Stop()
Assert 'restart:finished-target-returns-at-once' ($watch.Elapsed.TotalSeconds -lt 1.5)
Add-Type -AssemblyName System.Windows.Forms | Out-Null
$tray=$null; $menu=$null
try{
    [void](Call $program 'BuildTray' $null @())
    $tray=$program.GetField('tray',$flags).GetValue($null); $menu=$program.GetField('trayMenu',$flags).GetValue($null)
    $labels=@($menu.Items | ForEach-Object { $_.Text })
    $restartAt=[Array]::IndexOf($labels,$restartLabel)
    Assert 'tray:restart-item-present' ($restartAt -ge 0)
    Assert 'tray:restart-just-before-exit' ($restartAt -eq ($labels.Count-2) -and $labels[$labels.Count-1] -ne '')
}finally{
    if($tray -ne $null){$tray.Visible=$false; $tray.Dispose()}
    if($menu -ne $null){$menu.Dispose()}
    $program.GetField('tray',$flags).SetValue($null,$null); $program.GetField('trayMenu',$flags).SetValue($null,$null)
}
$window=$windowType.GetConstructor($flags,$null,[Type[]]@($cfgType,[string]),$null).Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$cfgPath))
$program.GetField('mainWindow',$flags).SetValue($null,$window)
[void](Call $program 'PrepareMainWindow' $null @($window,$true))
Assert 'startup:hidden-handle-ready' ((New-Object Windows.Interop.WindowInteropHelper($window)).Handle -ne [IntPtr]::Zero -and -not $window.IsVisible)
$cfg.AutoClearSeconds=60
$request=[long]$clipboardType.GetField('clipboardRequest',$flags).GetValue($clipboard)
[void](Call $clipboardType 'CompleteClipboardClear' $clipboard @($true,[uint32]123,$request))
$clear=$clipboardType.GetField('clearTimer',$flags).GetValue($clipboard)
Assert 'retention:successful-copy-still-schedules-clear' ($null -ne $clear -and $clear.IsEnabled)
$clear.Stop();$clipboardType.GetField('clearTimer',$flags).SetValue($clipboard,$null)
[void](Call $clipboardType 'CompleteClipboardClear' $clipboard @($true,[uint32]123,($request-1)))
Assert 'retention:superseded-copy-not-cleared' ($null -eq $clipboardType.GetField('clearTimer',$flags).GetValue($clipboard))
$cfg.AutoClearSeconds=0
$retention=$window.Content.FindName('txtHistoryRetention')
Assert 'retention:record-page-default-visible' ($retention.Text -eq '10')
$window.Content.FindName('txtEmailPoll').Text='invalid'
$retention.Text='20';Assert 'retention:independent-save' (Call $windowType 'SaveHistoryRetention' $window @())
Assert 'retention:field-persisted' ((Call $cfgType 'Load' $null @($cfgPath,$true)).HistoryRetentionMinutes -eq 20)
$retention.Text='-1';Assert 'retention:invalid-ui-rejected' (-not (Call $windowType 'SaveHistoryRetention' $window @()))
Assert 'retention:invalid-ui-draft-kept' ($retention.Text -eq '-1' -and $cfg.HistoryRetentionMinutes -eq 20)
$retention.Text='30';[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::ReadOnly)
try{Assert 'retention:failed-setting-save-keeps-draft' (-not (Call $windowType 'SaveHistoryRetention' $window @()) -and $cfg.HistoryRetentionMinutes -eq 20 -and $retention.Text -eq '30')}finally{[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::Normal)}
$window.Content.FindName('txtEmailPoll').Text='30'
$application=New-Object Windows.Application;$application.ShutdownMode=[Windows.ShutdownMode]::OnExplicitShutdown
$timer=New-Object Windows.Threading.DispatcherTimer;$timer.Interval=[TimeSpan]::FromMilliseconds(100)
$timer.Add_Tick([EventHandler]{
 $timer.Stop();Assert 'startup:hidden-before-user-open' (-not $window.IsVisible -and -not $window.ShowInTaskbar)
 $window.ShowActivated=$false;$window.Show()
 Assert 'startup:hidden-window-can-open' $window.IsVisible
 $retention.Text='15'
 [void](Call $windowType 'ExitApp' $window @());$application.Shutdown()
})
$timer.Start();[void](Call $program 'RunMainWindow' $null @($application,$window,$true))
Assert 'retention:focused-value-saved-on-exit' ((Call $cfgType 'Load' $null @($cfgPath,$true)).HistoryRetentionMinutes -eq 15)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0){exit 1}
