# Synthetic fixtures only; never changes the user's startup registry entry.
param([string]$ExePath,[string]$FixtureDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath)); $flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$cfgType=$asm.GetType('Codepass.Config'); $program=$asm.GetType('Codepass.Program'); $history=$asm.GetType('Codepass.History')
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
function AddItem([string]$channel,[DateTime]$received,[string]$code,[string]$key='') {
 $item=[Activator]::CreateInstance($itemType,$true);$item.Channel=$channel;$item.Time=$received.ToLocalTime().ToString('MM-dd HH:mm:ss')
 $item.ReceivedUtcTicks=$received.Ticks;$item.Code=$code;$item.MessageKey=$key;$item.Source='Synthetic';$item.Raw='Synthetic only'
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
Assert 'startup:manual-not-silent' (-not (Call $program 'IsSilentStartup' $null @(,[string[]]@('--dry'))))
Assert 'startup:argument-case-insensitive' (Call $program 'IsSilentStartup' $null @(,[string[]]@('--STARTUP')))
Assert 'startup:quoted-command-with-silent-flag' ((Call $program 'StartupCommand' $null @('D:\Synthetic Folder\codepass.exe')) -eq '"D:\Synthetic Folder\codepass.exe" --startup')
$window=$windowType.GetConstructor($flags,$null,[Type[]]@($cfgType,[string]),$null).Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$cfgPath))
$program.GetField('mainWindow',$flags).SetValue($null,$window)
[void](Call $program 'PrepareMainWindow' $null @($window,$true))
Assert 'startup:hidden-handle-ready' ((New-Object Windows.Interop.WindowInteropHelper($window)).Handle -ne [IntPtr]::Zero -and -not $window.IsVisible)
$cfg.AutoClearSeconds=60
$request=[long]$program.GetField('clipboardRequest',$flags).GetValue($null)
[void](Call $program 'CompleteClipboardClear' $null @($true,[uint32]123,$request))
$clear=$program.GetField('clearTimer',$flags).GetValue($null)
Assert 'retention:successful-copy-still-schedules-clear' ($null -ne $clear -and $clear.IsEnabled)
$clear.Stop();$program.GetField('clearTimer',$flags).SetValue($null,$null)
[void](Call $program 'CompleteClipboardClear' $null @($true,[uint32]123,($request-1)))
Assert 'retention:superseded-copy-not-cleared' ($null -eq $program.GetField('clearTimer',$flags).GetValue($null))
$cfg.AutoClearSeconds=0
$retention=$window.Content.FindName('txtHistoryRetention')
Assert 'retention:record-page-default-visible' ($retention.Text -eq '10')
$window.Content.FindName('txtEmailPoll').Text='invalid'
$retention.Text='20';Assert 'retention:independent-save' (Call $windowType 'SaveHistoryRetention' $window @())
Assert 'retention:field-persisted' ((Call $cfgType 'Load' $null @($cfgPath,$true)).HistoryRetentionMinutes -eq 20)
$retention.Text='-1';Assert 'retention:invalid-ui-rejected' (-not (Call $windowType 'SaveHistoryRetention' $window @()))
Assert 'retention:invalid-ui-restored' ($retention.Text -eq '20')
$retention.Text='30';[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::ReadOnly)
try{Assert 'retention:failed-setting-save-restored' (-not (Call $windowType 'SaveHistoryRetention' $window @()) -and $cfg.HistoryRetentionMinutes -eq 20 -and $retention.Text -eq '20')}finally{[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::Normal)}
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
