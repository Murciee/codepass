# Synthetic delivery and UI contracts only; no live network, clipboard or credentials.
param([string]$ExePath,[string]$FixtureDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type -TypeDefinition @'
using System;
using System.Threading;
public static class SyntheticClipboard {
 public static int Attempts;
 public static IntPtr Owner;
 public static ApartmentState Apartment;
 public static bool AlwaysBusy;
 public static readonly ManualResetEvent Attempted = new ManualResetEvent(false);
 public static bool Write(string text, IntPtr owner, out uint sequence, out string error) {
  int attempt=Interlocked.Increment(ref Attempts); Owner=owner; Apartment=Thread.CurrentThread.GetApartmentState();
  Attempted.Set();
  if(AlwaysBusy){sequence=0;error="synthetic busy";return false;}
  sequence=attempt<2?0U:101U; error=attempt<2?"synthetic busy":null; return attempt>=2;
 }
}
'@
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$root=[IO.Path]::GetFullPath($FixtureDirectory)
New-Item -ItemType Directory -Path $root | Out-Null
$pass=0;$fail=0
function Assert([string]$name,[bool]$ok){if($ok){$script:pass++;Write-Output ('PASS '+$name)}else{$script:fail++;Write-Output ('FAIL '+$name)}}
function Call($type,[string]$name,$target,[object[]]$values){
 $args=New-Object object[] $values.Length
 for($i=0;$i -lt $values.Length;$i++){if($null -ne $values[$i]){$args[$i]=$values[$i].PSObject.BaseObject}}
 if($null -ne $target){$target=$target.PSObject.BaseObject}
 $method=$type.GetMethods($flags)|Where-Object{$_.Name -eq $name -and $_.GetParameters().Length -eq $values.Length}|Select-Object -First 1
 if($null -eq $method){throw ('Missing '+$name)}
 return ,($method.Invoke($target,$args))
}
function Rejected([scriptblock]$action){try{& $action|Out-Null;return $false}catch{return $true}}
function Pump {
 $frame=New-Object Windows.Threading.DispatcherFrame
 [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{$frame.Continue=$false})
 [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function NewInstance($type,[object[]]$ctorValues=@()){
 if($ctorValues.Length -eq 0){return [Activator]::CreateInstance($type,$true)}
 $parameters=New-Object object[] $ctorValues.Length
 for($i=0;$i -lt $ctorValues.Length;$i++){if($null -ne $ctorValues[$i]){$parameters[$i]=$ctorValues[$i].PSObject.BaseObject}}
 return [Activator]::CreateInstance($type,$flags,$null,$parameters,$null)
}
$program=$asm.GetType('Codepass.Program');$config=$asm.GetType('Codepass.Config');$history=$asm.GetType('Codepass.History')
$delivery=$program.GetField('Incoming',$flags).GetValue($null);$deliveryType=$delivery.GetType()
$ntfy=$program.GetField('Ntfy',$flags).GetValue($null);$ntfyType=$ntfy.GetType()
$clipboard=$program.GetField('Clipboard',$flags).GetValue($null);$clipboardType=$clipboard.GetType()
$receipts=$history.GetField('Receipts',$flags).GetValue($null)
$identity=$asm.GetType('Codepass.MessageIdentity');$checkpointType=$asm.GetType('Codepass.NtfyCheckpoint')
$cfg=NewInstance $config
$cfg.EmailEnabled=$false;$cfg.EmailAutoCopy=$false;$cfg.EmailNotify=$false;$cfg.HistoryRetentionMinutes=0;$cfg.PcIp='192.0.2.5'
$program.GetField('cfg',$flags).SetValue($null,$cfg)
$program.GetField('appDir',$flags).SetValue($null,$root)
$path=Join-Path $root 'history.txt';[void](Call $history 'Init' $null @($path))
$now=[DateTime]::UtcNow;$unix=Call $identity 'UnixTime' $null @($now)
function Sms([string]$body,[string]$id,[DateTime]$when){return Call $deliveryType 'ReceiveSms' $delivery @($body,'http','Synthetic',$id,$when,$false,$false,$null)}
function Count{ return (Call $history 'Snapshot' $null @()).Count }
Assert 'sms:default-accept-keywords-present' ($cfg.SmsAcceptKeywords.Contains('验证码') -and $cfg.SmsAcceptKeywords.Contains('OTP'))
Assert 'sms:first-received' ((Sms 'verification code 234567' 'message-a' $now).ToString() -eq 'Received')
Assert 'sms:stable-id-dedup' ((Sms 'verification code 234567' 'message-a' $now).ToString() -eq 'Duplicate' -and (Count) -eq 1)
Assert 'sms:same-code-different-message-kept' ((Sms 'verification code 234567' 'message-b' $now).ToString() -eq 'Received')
[void](Sms 'verification code 345678' '' $now)
Assert 'sms:legacy-cross-channel-dedup' ((Call $deliveryType 'ReceiveSms' $delivery @('verification code 345678','ntfy','Other','',$now,$false,$false,$null)).ToString() -eq 'Duplicate')
Assert 'sms:old-replay-ignored' ((Sms 'verification code 456789' 'old' $now.AddMinutes(-11)).ToString() -eq 'Ignored')
$cfg.SmsAcceptKeywords='verification'
Assert 'sms:accept-gate-blocks-unmatched' ((Sms 'order 246810 shipped' 'accept-a' $now).ToString() -eq 'Ignored' -and (Count) -eq 3)
Assert 'sms:accept-gate-passes-matched' ((Sms 'verification code 246811' 'accept-b' $now).ToString() -eq 'Received')
Assert 'sms:accept-gate-case-insensitive' ((Sms 'VERIFICATION CODE 246812' 'accept-c' $now).ToString() -eq 'Received')
$cfg.SmsAcceptKeywords=''
Assert 'sms:accept-empty-accepts-all' ((Sms 'order 246813 shipped' 'accept-d' $now).ToString() -eq 'Received')
$wire=@{codepass=1;id='envelope-a';time=$unix;sender_b64='';body_b64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('verification code 567890'))}|ConvertTo-Json -Compress
Assert 'sms:empty-sender-envelope-supported' ((Sms $wire '' $now).ToString() -eq 'Received')
[IO.File]::SetAttributes($path,[IO.FileAttributes]::ReadOnly)
try{Assert 'sms:storage-failure-not-acknowledged' (Rejected{Sms 'verification code 678901' 'retry-a' $now})}finally{[IO.File]::SetAttributes($path,[IO.FileAttributes]::Normal)}
Assert 'sms:storage-repair-retry-received' ((Sms 'verification code 678901' 'retry-a' $now).ToString() -eq 'Received')
[void](Call $history 'Init' $null @($path))
Assert 'sms:dedup-survives-restart' ((Sms $wire '' $now).ToString() -eq 'Duplicate')
# A duplicate arriving on a second transport must persist its receipt atomically.
$before=Count;$transportKey='ntfy:synthetic-second-transport'
[IO.File]::SetAttributes($path,[IO.FileAttributes]::ReadOnly)
try { Assert 'receipts:duplicate-transport-write-failure-visible' (Rejected { Call $deliveryType 'ReceiveSms' $delivery @('verification code 678901','ntfy','Synthetic','retry-a',$now,$false,$false,$transportKey) }) }
finally { [IO.File]::SetAttributes($path,[IO.FileAttributes]::Normal) }
$recent=$receipts.GetType().GetField('RecentMessages',$flags).GetValue($receipts)
Assert 'receipts:duplicate-transport-failure-restores-memory' (-not $recent.ContainsKey($transportKey) -and (Count) -eq $before)
Assert 'receipts:duplicate-transport-retry-commits' ((Call $deliveryType 'ReceiveSms' $delivery @('verification code 678901','ntfy','Synthetic','retry-a',$now,$false,$false,$transportKey)).ToString() -eq 'Duplicate')
[void](Call $history 'Init' $null @($path))
Assert 'receipts:duplicate-transport-survives-restart' ($recent.ContainsKey($transportKey) -and (Count) -eq $before)
$mail=[Activator]::CreateInstance($asm.GetType('Codepass.HistoryItem'),$true)
$mail.Channel='email';$mail.Code='876543';$mail.MessageKey='synthetic-pending-rollback';$mail.MailboxKey='synthetic-mailbox';$mail.MailUid=123
[IO.File]::SetAttributes($path,[IO.FileAttributes]::ReadOnly)
try { Assert 'receipts:new-mail-storage-failure-visible' (Rejected { Call $history 'AddUnique' $null @($mail) }) }
finally { [IO.File]::SetAttributes($path,[IO.FileAttributes]::Normal) }
$pending=$receipts.GetType().GetField('PendingReceipts',$flags).GetValue($receipts)
Assert 'receipts:new-mail-failure-restores-both-ledgers' (-not $pending.ContainsKey($mail.MessageKey) -and -not $recent.ContainsKey($mail.MessageKey) -and (Count) -eq $before)
[void](Call $history 'Init' $null @($path))
Assert 'receipts:new-mail-failure-keeps-disk' (-not $pending.ContainsKey($mail.MessageKey) -and (Count) -eq $before)
Assert 'receipts:new-mail-retry-commits-all' ((Call $history 'AddUnique' $null @($mail)) -and $pending.ContainsKey($mail.MessageKey) -and $recent.ContainsKey($mail.MessageKey))
$checkpointPath=Join-Path $root 'ntfy-state.txt'
$checkpoint=NewInstance $checkpointType @($checkpointPath,'https://example.invalid','synthetic-topic')
function Event([string]$id,[string]$body){return (@{event='message';id=$id;time=$unix;message=$body}|ConvertTo-Json -Compress)}
Assert 'ntfy:malformed-event-skipped' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @('{bad',$checkpoint)).ToString() -eq 'Ignored')
$bad=Event 'bad-body' '{"codepass":1,"id":"bad","body_b64":"!","time":1}'
Assert 'ntfy:malformed-payload-skipped' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @($bad,$checkpoint)).ToString() -eq 'Ignored')
Assert 'ntfy:malformed-payload-confirmed' (Call $checkpointType 'Contains' $checkpoint @('bad-body'))
$first=Event 'same-second-a' 'verification code 789012'
$second=Event 'same-second-b' 'verification code 890123'
Assert 'ntfy:first-event-received' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @($first,$checkpoint)).ToString() -eq 'Received')
[IO.File]::SetAttributes($path,[IO.FileAttributes]::ReadOnly)
try{Assert 'ntfy:second-storage-failure-retryable' (Rejected{Call $ntfyType 'ProcessNtfyLine' $ntfy @($second,$checkpoint)})}finally{[IO.File]::SetAttributes($path,[IO.FileAttributes]::Normal)}
$checkpoint=NewInstance $checkpointType @($checkpointPath,'https://example.invalid','synthetic-topic')
[void](Call $history 'Init' $null @($path))
Assert 'ntfy:same-second-first-survives-restart' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @($first,$checkpoint)).ToString() -eq 'Duplicate')
Assert 'ntfy:same-second-second-not-lost' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @($second,$checkpoint)).ToString() -eq 'Received')
Assert 'ntfy:overlap-boundary-second' ((Call $checkpointType 'Since' $checkpoint @($now)) -lt $unix)
$third=Event 'checkpoint-failure' 'verification code 901234'
[IO.File]::SetAttributes($checkpointPath,[IO.FileAttributes]::ReadOnly)
try{Assert 'ntfy:checkpoint-failure-visible' (Rejected{Call $ntfyType 'ProcessNtfyLine' $ntfy @($third,$checkpoint)})}finally{[IO.File]::SetAttributes($checkpointPath,[IO.FileAttributes]::Normal)}
$before=Count
# Expire only the legacy body fingerprint, leaving the transport receipt durable.
$recent=$receipts.GetType().GetField('RecentMessages',$flags).GetValue($receipts)
$bodyKey=Call $identity 'SmsKey' $null @('verification code 901234','')
$recent[$bodyKey].GetType().GetField('Expires',$flags).SetValue($recent[$bodyKey],[DateTime]::UtcNow.AddMinutes(-1).Ticks)
$recent[$bodyKey].GetType().GetField('ProtectedLine',$flags).SetValue($recent[$bodyKey],$null)
foreach($item in (Call $history 'Snapshot' $null @())){if($item.MessageKey -eq $bodyKey){$item.ReceivedUtcTicks=[DateTime]::UtcNow.AddMinutes(-3).Ticks}}
[void](Call $history 'RetrySave' $null @())
[void](Call $history 'Init' $null @($path))
$checkpoint=NewInstance $checkpointType @($checkpointPath,'https://example.invalid','synthetic-topic')
Assert 'ntfy:checkpoint-failure-does-not-redeliver' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @($third,$checkpoint)).ToString() -eq 'Duplicate' -and (Count) -eq $before)
$other=NewInstance $checkpointType @($checkpointPath,'https://example.invalid','other-topic')
Assert 'ntfy:topic-checkpoint-isolated' (-not (Call $checkpointType 'Contains' $other @('same-second-a')))
$timePath=Join-Path $root 'time-state.txt'
$timeCheckpoint=NewInstance $checkpointType @($timePath,'https://example.invalid','time-topic')
[void](Call $checkpointType 'Acknowledge' $timeCheckpoint @(([long]$unix-120),'older'))
$timeBefore=Call $checkpointType 'Since' $timeCheckpoint @($now)
$invalidTime=@{event='message';id='bad-time';time=[long]::MaxValue;message='verification code 998877'}|ConvertTo-Json -Compress
Assert 'ntfy:invalid-time-isolated' ((Call $ntfyType 'ProcessNtfyLine' $ntfy @($invalidTime,$timeCheckpoint)).ToString() -eq 'Ignored')
$timeCheckpoint=NewInstance $checkpointType @($timePath,'https://example.invalid','time-topic')
Assert 'ntfy:invalid-time-does-not-advance-restart' ((Call $checkpointType 'Since' $timeCheckpoint @($now)) -eq $timeBefore)
$shutdownEvent=Event 'shutdown-uncommitted' 'verification code 102938'
$before=Count
$program.GetField('running',$flags).SetValue($null,$false)
try { try { [void](Call $ntfyType 'ProcessNtfyLine' $ntfy @($shutdownEvent,$timeCheckpoint)) } catch { } }
finally { $program.GetField('running',$flags).SetValue($null,$true) }
Assert 'ntfy:shutdown-does-not-record' ((Count) -eq $before)
$timeCheckpoint=NewInstance $checkpointType @($timePath,'https://example.invalid','time-topic')
Assert 'ntfy:shutdown-does-not-confirm-uncommitted-event' (-not (Call $checkpointType 'Contains' $timeCheckpoint @('shutdown-uncommitted')))
# Exercise the extracted receiver with loopback-only synthetic HTTP; no external network.
$http=$program.GetField('Http',$flags).GetValue($null);$httpType=$http.GetType()
$portProbe=New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback,0)
$portProbe.Start();$cfg.Port=$portProbe.LocalEndpoint.Port;$portProbe.Stop()
$cfg.Token='synthetic-http-token';$cfg.PcIp='127.0.0.1'
function HttpRequest([string]$request) {
 $client=New-Object Net.Sockets.TcpClient
 try {
  $client.Connect([Net.IPAddress]::Loopback,$cfg.Port);$client.ReceiveTimeout=2500;$client.SendTimeout=2500
  $stream=$client.GetStream();$bytes=[Text.Encoding]::UTF8.GetBytes($request);$stream.Write($bytes,0,$bytes.Length)
  $result=New-Object IO.MemoryStream;$buffer=New-Object byte[] 4096
  try { while(($count=$stream.Read($buffer,0,$buffer.Length)) -gt 0){$result.Write($buffer,0,$count)} } catch [IO.IOException] { }
  return [Text.Encoding]::UTF8.GetString($result.ToArray())
 } finally { $client.Close() }
}
[void](Call $httpType 'Start' $http @())
try {
 $deadline=[DateTime]::UtcNow.AddSeconds(3)
 do { try{$health=HttpRequest "GET /health HTTP/1.1`r`nX-Token: synthetic-http-token`r`n`r`n"}catch{$health=''};if(-not $health){Start-Sleep -Milliseconds 20} } while(-not $health -and [DateTime]::UtcNow -lt $deadline)
 Assert 'http:receiver-starts-loopback-health' ($health.StartsWith('HTTP/1.1 200'))
 Assert 'http:missing-auth-rejected' ((HttpRequest "GET /health HTTP/1.1`r`n`r`n").StartsWith('HTTP/1.1 403'))
 Assert 'http:query-key-must-match-exactly' ((HttpRequest "GET /health?xtoken=synthetic-http-token HTTP/1.1`r`n`r`n").StartsWith('HTTP/1.1 403'))
 $before=Count;$body='verification code 667788'
 $request="POST /sms HTTP/1.1`r`nX-Token: synthetic-http-token`r`nX-SMS-Id: receiver-fixture`r`nContent-Length: $($body.Length)`r`n`r`n$body"
 Assert 'http:message-committed-before-success' ((HttpRequest $request).StartsWith('HTTP/1.1 200') -and (Count) -eq $before+1)
 Assert 'http:repeat-request-acknowledged-once' ((HttpRequest $request).StartsWith('HTTP/1.1 200') -and (Count) -eq $before+1)
 [IO.File]::SetAttributes($path,[IO.FileAttributes]::ReadOnly)
 try { Assert 'http:failed-storage-not-successful' (-not (HttpRequest ($request.Replace('receiver-fixture','receiver-retry'))).StartsWith('HTTP/1.1 200') -and (Count) -eq $before+1) }
 finally { [IO.File]::SetAttributes($path,[IO.FileAttributes]::Normal) }
 Assert 'http:storage-repair-retry-successful' ((HttpRequest ($request.Replace('receiver-fixture','receiver-retry'))).StartsWith('HTTP/1.1 200') -and (Count) -eq $before+2)
 $blockedClient=New-Object Net.Sockets.TcpClient;$blockedClient.Connect([Net.IPAddress]::Loopback,$cfg.Port)
 $blockedStream=$blockedClient.GetStream();$blockedClient.ReceiveTimeout=2000
 $partial=[Text.Encoding]::ASCII.GetBytes("POST /sms HTTP/1.1`r`nContent-Length: 200`r`n`r`n")
 $blockedStream.Write($partial,0,$partial.Length)
} finally { [void](Call $httpType 'Stop' $http @()) }
Assert 'http:stop-joins-listener' (-not $httpType.GetField('thread',$flags).GetValue($http).IsAlive)
$closed=$false
try {$closed=$blockedStream.ReadByte() -eq -1} catch [IO.IOException] {$closed=$_.Exception.InnerException -is [Net.Sockets.SocketException] -and $_.Exception.InnerException.SocketErrorCode -ne [Net.Sockets.SocketError]::TimedOut}
$blockedClient.Close()
Assert 'http:stop-closes-partial-request' $closed
$cfg.Token='';$cfg.PcIp='192.0.2.5'
$cfgPath=Join-Path $root 'config.ini';$cfg.Save($cfgPath)
$windowType=$asm.GetType('Codepass.MainWindow');$window=NewInstance $windowType @($cfg,$cfgPath)
$content=$window.Content
function ApplySection([string]$name){return Call $windowType 'ApplySection' $window @([Enum]::Parse($asm.GetType('Codepass.SettingsSection'),$name))}
$content.FindName('txtEmailPoll').Text='bad'
$content.FindName('cmbPrivacy').SelectedIndex=1
[void](Call $windowType 'FlushSettings' $window @())
Assert 'settings:privacy-not-blocked-by-email-draft' ($cfg.PrivacyMode -eq 1)
$content.FindName('txtSmsMaxLength').Text='0'
Assert 'settings:invalid-draft-preserved' (-not (ApplySection 'SmsRules') -and $content.FindName('txtSmsMaxLength').Text -eq '0' -and $cfg.SmsMaxLength -eq 500)
$content.FindName('txtSmsMaxLength').Text='600'
Assert 'settings:valid-sms-not-blocked-by-email-draft' (ApplySection 'SmsRules')
$content.FindName('txtEmailPoll').Text='30'
$content.FindName('txtEmailAccount').Text='draft@example.invalid'
Assert 'settings:email-draft-not-autosaved' ($cfg.EmailAccount -eq '')
Assert 'settings:email-explicit-save' ((ApplySection 'Email') -and $cfg.EmailAccount -eq 'draft@example.invalid')
$cfg.EmailMailboxState='synthetic-watermark'
$draft=NewInstance $config;$draft.CopyFrom($cfg);$draft.PrivacyMode=2;$draft.EmailMailboxState='stale'
[void](Call $asm.GetType('Codepass.SettingsCommit') 'Commit' $null @($cfg,$draft,[Enum]::Parse($asm.GetType('Codepass.SettingsSection'),'Privacy'),$cfgPath))
Assert 'settings:commit-preserves-new-watermark' ($cfg.EmailMailboxState -eq 'synthetic-watermark')
$queue=$windowType.GetField('settingsQueue',$flags).GetValue($window)
$sectionType=$asm.GetType('Codepass.SettingsSection');$emailRules=[Enum]::Parse($sectionType,'EmailRules')
$content.FindName('txtEmailPoll').Text='45'
[void](Call $windowType 'QueueSection' $window @($emailRules))
[void](Call $queue.GetType() 'Drain' $queue @())
$content.FindName('txtEmailPoll').Text='bad'
Assert 'queue:invalid-attempt-rejected' (-not (Call $windowType 'QueueSection' $window @($emailRules)))
Pump
$errors=$windowType.GetField('sectionErrors',$flags).GetValue($window)
Assert 'queue:old-success-cannot-clear-new-error' ($errors.ContainsKey($emailRules) -and $content.FindName('txtEmailPoll').Text -eq 'bad' -and $cfg.EmailPollSeconds -eq 45)
$content.FindName('txtEmailPoll').Text='46'
[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::ReadOnly)
try{
 [void](Call $windowType 'QueueSection' $window @($emailRules))
 [void](Call $queue.GetType() 'Drain' $queue @())
}finally{[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::Normal)}
$content.FindName('txtEmailPoll').Text='47'
Assert 'queue:new-combined-commit-succeeds' (ApplySection 'All')
Pump
Assert 'queue:old-failure-cannot-overwrite-new-commit' ($cfg.EmailPollSeconds -eq 47 -and -not $errors.ContainsKey($emailRules))
$window.ShowActivated=$false;$window.Show()
Assert 'tests:current-result-visible' (Call $windowType 'CanShowTestResult' $window @(2,2))
Assert 'tests:stale-result-suppressed' (-not (Call $windowType 'CanShowTestResult' $window @(1,2)))
$content.FindName('lockOverlay').Visibility=[Windows.Visibility]::Visible
Assert 'tests:locked-result-suppressed' (-not (Call $windowType 'CanShowTestResult' $window @(2,2)))
$content.FindName('lockOverlay').Visibility=[Windows.Visibility]::Collapsed
$passwordType=$asm.GetType('Codepass.PasswordSettingsDialog')
Assert 'password:wrong-current-rejected' ($null -ne (Call $passwordType 'Validate' $null @($true,$false,$false,'abcdefgh','abcdefgh')))
Assert 'password:mismatch-rejected' ($null -ne (Call $passwordType 'Validate' $null @($true,$true,$false,'abcdefgh','abcdxxxx')))
Assert 'password:valid-change' ($null -eq (Call $passwordType 'Validate' $null @($true,$true,$false,'abcdefgh','abcdefgh')))
$passwordDialog=NewInstance $passwordType @($window,$cfg,$cfgPath,$false)
$passwordType.GetField('newPassword',$flags).GetValue($passwordDialog).Password='synthetic-password'
$passwordType.GetField('confirmation',$flags).GetValue($passwordDialog).Password='synthetic-password'
$oldHash=$cfg.LockHash
[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::ReadOnly)
try{[void](Call $passwordType 'Confirm' $passwordDialog @())}finally{[IO.File]::SetAttributes($cfgPath,[IO.FileAttributes]::Normal)}
Assert 'password:failed-write-preserves-lock' ($cfg.LockHash -eq $oldHash -and $passwordType.GetField('error',$flags).GetValue($passwordDialog).Text.Length -gt 0)
$passwordDialog.Close()
$list=$content.FindName('listHost');$firstRow=$list.Children[0]
$historyTimerBeforeHide=$windowType.GetField('historyRefreshTimer',$flags).GetValue($window)
$windowTimerBeforeHide=$windowType.GetField('windowSizeTimer',$flags).GetValue($window)
$toastTimerBeforeHide=$windowType.GetField('toastTimer',$flags).GetValue($window)
$contentBeforeHide=$window.Content
$emailAccountBeforeHide=$content.FindName('txtEmailAccount')
$handleBeforeHide=(New-Object Windows.Interop.WindowInteropHelper($window)).Handle
[void](Call $windowType 'Close' $window @())
$historyTimerAfterHide=$windowType.GetField('historyRefreshTimer',$flags).GetValue($window)
$windowTimerAfterHide=$windowType.GetField('windowSizeTimer',$flags).GetValue($window)
$toastTimerAfterHide=$windowType.GetField('toastTimer',$flags).GetValue($window)
Assert 'history:hidden-window-is-not-visible' (-not $window.IsVisible -and -not $window.ShowInTaskbar)
Assert 'history:hidden-keeps-content-tree' ([object]::ReferenceEquals($contentBeforeHide,$window.Content) -and [object]::ReferenceEquals($emailAccountBeforeHide,$content.FindName('txtEmailAccount')))
Assert 'history:hidden-releases-dynamic-rows' ($list.Children.Count -eq 0 -and $null -eq $firstRow.Parent)
Assert 'history:hidden-marks-list-dirty' ($windowType.GetField('historyDirty',$flags).GetValue($window))
$historyTimerStopped=$null -eq $historyTimerBeforeHide -or -not $historyTimerBeforeHide.IsEnabled
$windowTimerStopped=$null -eq $windowTimerBeforeHide -or -not $windowTimerBeforeHide.IsEnabled
$toastTimerStopped=$null -eq $toastTimerBeforeHide -or -not $toastTimerBeforeHide.IsEnabled
Assert 'history:hidden-stops-page-timers' ($historyTimerStopped -and $windowTimerStopped -and $toastTimerStopped -and $null -eq $historyTimerAfterHide -and $null -eq $windowTimerAfterHide -and $null -eq $toastTimerAfterHide)
Assert 'history:hidden-preserves-window-handle' ((New-Object Windows.Interop.WindowInteropHelper($window)).Handle -eq $handleBeforeHide)
[void](Call $windowType 'OnNewRecord' $window @((Call $history 'Snapshot' $null @())[0]))
Assert 'history:hidden-new-record-does-not-rebuild' ($list.Children.Count -eq 0)
Assert 'history:latest-action-present' ($null -ne $content.FindName('btnCopyLatest'))
Assert 'layout:advanced-default-collapsed' (-not $content.FindName('emailAdvanced').IsExpanded)
[void](Call $windowType 'SelectSection' $window @('general'))
[void](Call $windowType 'ShowFromTray' $window @())
Pump
Assert 'history:show-from-tray-restores-window' ($window.IsVisible -and $window.ShowInTaskbar -and $window.WindowState -eq [Windows.WindowState]::Normal)
Assert 'history:show-from-tray-keeps-handle' ((New-Object Windows.Interop.WindowInteropHelper($window)).Handle -eq $handleBeforeHide)
Assert 'history:non-history-page-defers-rebuild' ($list.Children.Count -eq 0 -and $windowType.GetField('historyDirty',$flags).GetValue($window))
[void](Call $windowType 'SelectSection' $window @('history'))
Assert 'history:show-from-tray-rebuilds-list' ($list.Children.Count -gt 0 -and $windowType.GetField('historyDirty',$flags).GetValue($window) -eq $false)
$field=$windowType.GetField('fldEmailAuth',$flags).GetValue($window)
$secret=$field.GetType();$field.Text='synthetic-auth'
$version=$windowType.GetField('emailDraftVersion',$flags).GetValue($window)
$toggle=$secret.GetField('toggle',$flags).GetValue($field)
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Assert 'secret:show-hide-does-not-change-draft' ($version -eq $windowType.GetField('emailDraftVersion',$flags).GetValue($window))
$content.FindName('txtEmailAccount').Text='unsaved@example.invalid'
$content.FindName('cmbPrivacy').SelectedIndex=0
[void](Call $windowType 'FlushSettings' $window @())
[void](Call $windowType 'SettingsFeedback' $window @('Saved',$false))
Assert 'settings:unrelated-save-keeps-email-draft-notice' ($content.FindName('lblSettingsFeedback').Text.Contains('邮箱连接草稿未保存'))
$appearance=$asm.GetType('Codepass.UiAppearance')
$prepared=Call $appearance 'Prepare' $null @($windowType.GetField('UiXaml',$flags).GetRawConstantValue(),$true,$false)
$accessible=[Windows.Markup.XamlReader]::Parse($prepared)
Assert 'appearance:system-template-parses' ($null -ne $accessible)
Assert 'appearance:reduced-motion-no-timed-keyframes' (-not $prepared.Contains('KeyTime="0:0:0.'))
# Synthetic latency observations are informational, not machine-dependent pass gates.
$durations=New-Object 'System.Collections.Generic.List[double]'
for($i=0;$i -lt 30;$i++){
 $clock=[Diagnostics.Stopwatch]::StartNew()
 [void](Sms ('verification code '+(700000+$i)) ('perf-'+$i) ([DateTime]::UtcNow))
 $clock.Stop();$durations.Add($clock.Elapsed.TotalMilliseconds)
}
$ordered=@($durations|Sort-Object)
Write-Output ('PERF sms-commit-p50-ms='+[Math]::Round($ordered[14],2)+' p95-ms='+[Math]::Round($ordered[28],2))
for($i=0;$i -lt 70;$i++){[void](Sms ('verification code '+(710000+$i)) ('perf-fill-'+$i) ([DateTime]::UtcNow))}
$window.Show()
$clock=[Diagnostics.Stopwatch]::StartNew();[void](Call $windowType 'RefreshHistory' $window @());$clock.Stop()
Write-Output ('PERF history-100-rebuild-ms='+[Math]::Round($clock.Elapsed.TotalMilliseconds,2))
Assert 'history:retains-100-record-bound' ((Count) -eq 100)
$cfg.SetPassword('synthetic-test-password')
[void](Call $windowType 'LockWindow' $window @())
Assert 'lock:business-tree-hidden-and-disabled' ($content.FindName('businessContent').Visibility -eq [Windows.Visibility]::Hidden -and -not $content.FindName('businessContent').IsEnabled)
Assert 'lock:keyboard-focus-contained' ([Windows.Input.KeyboardNavigation]::GetTabNavigation($content.FindName('lockOverlay')).ToString() -eq 'Cycle')
$content.FindName('txtUnlockPassword').Password='synthetic-test-password'
[void](Call $windowType 'TryUnlock' $window @())
Assert 'lock:unlock-restores-content' ($content.FindName('businessContent').Visibility -eq [Windows.Visibility]::Visible -and $content.FindName('businessContent').IsEnabled)
$window.Width=640;$window.Height=460
Pump
[void](Call $windowType 'UpdateCompactShell' $window @())
$content.Measure((New-Object Windows.Size(640,460)));$content.Arrange((New-Object Windows.Rect(0,0,640,460)));$content.UpdateLayout()
Assert 'layout:small-window-keeps-record-list' ($content.FindName('historyScroll').ActualHeight -ge 60)
Write-Output ('GEOMETRY small-list-height='+$content.FindName('historyScroll').ActualHeight+' actual-window='+$window.ActualWidth+'x'+$window.ActualHeight+' footer='+$content.FindName('historyFooter').ActualHeight)
$window.Width=1100;$window.Height=760
Pump
[void](Call $windowType 'UpdateCompactShell' $window @())
$content.Measure((New-Object Windows.Size(1100,760)));$content.Arrange((New-Object Windows.Rect(0,0,1100,760)));$content.UpdateLayout()
$scroll=$content.FindName('historyScroll');$scroll.ScrollToVerticalOffset(300);$content.UpdateLayout()
$content.FindName('btnNewRecords').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
$content.UpdateLayout()
Assert 'history:new-record-action-scrolls-top' ($scroll.VerticalOffset -eq 0)
# Existing history/clipboard handling is independent of a full main window.
$program.GetField('mainWindow',$flags).SetValue($null,$null)
Assert 'startup:headless-delivery-persists' ((Sms 'verification code 765432' 'headless' ([DateTime]::UtcNow)).ToString() -eq 'Received')
$app=New-Object Windows.Application;$app.ShutdownMode=[Windows.ShutdownMode]::OnExplicitShutdown
$program.GetField('app',$flags).SetValue($null,$app)
[void](Call $clipboardType 'Initialize' $clipboard @())
$owner=$clipboardType.GetField('clipboardOwner',$flags).GetValue($clipboard)
$writer=$clipboardType.GetField('WriteClipboard',$flags);$originalWriter=$writer.GetValue($clipboard)
$writer.SetValue($clipboard,[Delegate]::CreateDelegate($writer.FieldType,[SyntheticClipboard].GetMethod('Write')))
$cfg.AutoClearSeconds=60
[void](Call $deliveryType 'ReceiveSms' $delivery @('verification code 654321','http','Synthetic','headless-copy',[DateTime]::UtcNow,$true,$false,$null))
$deadline=[DateTime]::UtcNow.AddSeconds(3)
while([DateTime]::UtcNow -lt $deadline -and $null -eq $clipboardType.GetField('clearTimer',$flags).GetValue($clipboard)){Pump;Start-Sleep -Milliseconds 20}
Assert 'clipboard:headless-sta-retry-executed' ([SyntheticClipboard]::Attempts -ge 2 -and [SyntheticClipboard]::Apartment -eq [Threading.ApartmentState]::STA)
Assert 'clipboard:headless-owner-preserved' ([SyntheticClipboard]::Owner -eq $owner -and [SyntheticClipboard]::Owner -ne [IntPtr]::Zero)
$clear=$clipboardType.GetField('clearTimer',$flags).GetValue($clipboard)
Assert 'clipboard:completion-schedules-clear' ($null -ne $clear -and $clear.IsEnabled)
if($null -ne $clear){$clear.Stop()}
$clipboardType.GetField('clearTimer',$flags).SetValue($clipboard,$null)
[SyntheticClipboard]::Attempts=0
[void](Call $deliveryType 'ReceiveSms' $delivery @('verification code 543210','http','Synthetic','delete-before-dispatch',[DateTime]::UtcNow,$true,$false,$null))
$deleted=(Call $history 'Snapshot' $null @())[0]
[void](Call $history 'Remove' $null @($deleted))
Pump
Assert 'remove:queued-delivery-cancelled-before-copy' ([SyntheticClipboard]::Attempts -eq 0 -and (Call $program 'GetLatestReceipt' $null @()).Code -ne '543210')
[SyntheticClipboard]::AlwaysBusy=$true;[SyntheticClipboard]::Attempted.Reset()|Out-Null
[void](Call $deliveryType 'ReceiveSms' $delivery @('verification code 432109','http','Synthetic','delete-during-retry',[DateTime]::UtcNow,$true,$false,$null))
Pump
Assert 'remove:copy-retry-started' ([SyntheticClipboard]::Attempted.WaitOne(2000))
$deleted=(Call $history 'Snapshot' $null @())[0]
[void](Call $history 'Remove' $null @($deleted))
$attempts=[SyntheticClipboard]::Attempts
[SyntheticClipboard]::AlwaysBusy=$false
$deadline=[DateTime]::UtcNow.AddMilliseconds(750)
while([DateTime]::UtcNow -lt $deadline){Pump;Start-Sleep -Milliseconds 20}
Assert 'remove:deleted-record-stops-copy-retry' ([SyntheticClipboard]::Attempts -eq $attempts -and $null -eq $clipboardType.GetField('clearTimer',$flags).GetValue($clipboard))
Assert 'remove:cancelled-copy-reaches-terminal-state' ($asm.GetType('Codepass.RuntimeStatus').GetField('Clipboard',$flags).GetValue($null) -eq '复制已取消')
Assert 'remove:deleted-sms-is-not-redelivered' ((Sms 'verification code 432109' 'delete-during-retry' ([DateTime]::UtcNow)).ToString() -eq 'Duplicate')
[SyntheticClipboard]::AlwaysBusy=$true;[SyntheticClipboard]::Attempted.Reset()|Out-Null
[void](Call $deliveryType 'ReceiveSms' $delivery @('verification code 321098','http','Synthetic','delete-before-new-copy',[DateTime]::UtcNow,$true,$false,$null))
Pump
Assert 'remove:older-copy-started-before-replacement' ([SyntheticClipboard]::Attempted.WaitOne(2000))
$deleted=(Call $history 'Snapshot' $null @())[0]
[void](Call $history 'Remove' $null @($deleted))
[SyntheticClipboard]::AlwaysBusy=$false
Assert 'remove:newer-manual-copy-succeeds' (Call $clipboardType 'CopyManually' $clipboard @('synthetic-newer-copy',$owner))
$latestClipboard=$asm.GetType('Codepass.RuntimeStatus').GetField('Clipboard',$flags).GetValue($null)
$attempts=[SyntheticClipboard]::Attempts
$deadline=[DateTime]::UtcNow.AddMilliseconds(750)
while([DateTime]::UtcNow -lt $deadline){Pump;Start-Sleep -Milliseconds 20}
Assert 'remove:old-cancellation-cannot-overwrite-newer-copy' ([SyntheticClipboard]::Attempts -eq $attempts -and $asm.GetType('Codepass.RuntimeStatus').GetField('Clipboard',$flags).GetValue($null) -eq $latestClipboard)
[SyntheticClipboard]::AlwaysBusy=$true;[SyntheticClipboard]::Attempted.Reset()|Out-Null
[void](Call $deliveryType 'ReceiveSms' $delivery @('verification code 210987','http','Synthetic','clear-during-retry',[DateTime]::UtcNow,$true,$false,$null))
Pump
Assert 'clear:copy-retry-started' ([SyntheticClipboard]::Attempted.WaitOne(2000))
[void](Call $history 'Clear' $null @())
$attempts=[SyntheticClipboard]::Attempts
[SyntheticClipboard]::AlwaysBusy=$false
$deadline=[DateTime]::UtcNow.AddMilliseconds(750)
while([DateTime]::UtcNow -lt $deadline){Pump;Start-Sleep -Milliseconds 20}
Assert 'clear:pending-copy-cancelled' ([SyntheticClipboard]::Attempts -eq $attempts -and (Count) -eq 0)
Assert 'clear:receipt-survives-visible-clear' ((Sms 'verification code 210987' 'clear-during-retry' ([DateTime]::UtcNow)).ToString() -eq 'Duplicate')
[SyntheticClipboard]::AlwaysBusy=$true;[SyntheticClipboard]::Attempted.Reset()|Out-Null
[void](Call $deliveryType 'ReceiveSms' $delivery @('verification code 109876','http','Synthetic','exit-during-retry',[DateTime]::UtcNow,$true,$false,$null))
Pump
Assert 'clipboard:exit-retry-started' ([SyntheticClipboard]::Attempted.WaitOne(2000))
[void](Call $clipboardType 'Dispose' $clipboard @())
$attempts=[SyntheticClipboard]::Attempts
$deadline=[DateTime]::UtcNow.AddMilliseconds(750)
while([DateTime]::UtcNow -lt $deadline){Pump;Start-Sleep -Milliseconds 20}
Assert 'clipboard:exit-cancels-retry-and-owner' ([SyntheticClipboard]::Attempts -eq $attempts -and $clipboardType.GetField('clipboardOwner',$flags).GetValue($clipboard) -eq [IntPtr]::Zero)
Assert 'clipboard:disposed-service-rejects-copy' (-not (Call $clipboardType 'CopyManually' $clipboard @('synthetic-after-exit',$owner)))
$writer.SetValue($clipboard,$originalWriter)
# Immediate close drains the explicitly requested snapshot and records failures without a Dispatcher callback.
[void](Call $windowType 'FlushSettings' $window @())
$exitPath=Join-Path $root 'exit-config.ini';$exitCfg=NewInstance $config;$exitCfg.PcIp='192.0.2.6';$exitCfg.Save($exitPath)
$exitWindow=NewInstance $windowType @($exitCfg,$exitPath)
$exitWindow.Content.FindName('txtEmailAccount').Text='requested@example.invalid'
[IO.File]::SetAttributes($exitPath,[IO.FileAttributes]::ReadOnly)
try{
 [void](Call $windowType 'QueueSection' $exitWindow @([Enum]::Parse($sectionType,'Email')))
 [void](Call $windowType 'ExitApp' $exitWindow @())
}finally{[IO.File]::SetAttributes($exitPath,[IO.FileAttributes]::Normal)}
Assert 'queue:immediate-exit-retains-failure-record' ($windowType.GetField('settingsSaveFailureCount',$flags).GetValue($exitWindow) -gt 0 -and $exitCfg.EmailAccount -eq '')
Assert 'queue:immediate-exit-retains-email-error' ($windowType.GetField('sectionErrors',$flags).GetValue($exitWindow).ContainsKey([Enum]::Parse($sectionType,'Email')))
[void](Call $windowType 'ExitApp' $window @())
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0){exit 1}
