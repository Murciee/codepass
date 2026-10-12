# Synthetic config/history only; no live credentials, mailboxes, or clipboard writes.
param([string]$ExePath = (Join-Path $PSScriptRoot '..\dist\codepass.exe'), [string]$FixtureDirectory = '')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading;
public static class SmsTransactionProbe {
    public static bool InvokeAcrossRollback(MethodInfo incoming, object delivery, object config, FieldInfo limit, object gate, string text, int transient, int restored) {
        Exception failure = null;
        bool blocked;
        using (ManualResetEvent entered = new ManualResetEvent(false)) {
            Thread worker = new Thread(delegate() {
                entered.Set();
                try { incoming.Invoke(delivery, new object[] { text, "http", "Synthetic", false, false, false }); }
                catch (Exception ex) { failure = ex; }
            });
            worker.IsBackground = true;
            lock (gate) {
                limit.SetValue(config, transient);
                worker.Start();
                if (!entered.WaitOne(1000)) throw new Exception("SMS worker did not start");
                SpinWait.SpinUntil(delegate { return !worker.IsAlive || (worker.ThreadState & ThreadState.WaitSleepJoin) != 0; }, 1000);
                blocked = worker.IsAlive && (worker.ThreadState & ThreadState.WaitSleepJoin) != 0;
                limit.SetValue(config, restored);
            }
            if (!worker.Join(5000)) throw new Exception("SMS worker did not complete");
            if (failure != null) throw new Exception("SMS transaction probe failed", failure);
        }
        return blocked;
    }
}
'@
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$configType=$asm.GetType('Codepass.Config')
$windowType=$asm.GetType('Codepass.MainWindow')
$programType=$asm.GetType('Codepass.Program')
$delivery=$programType.GetField('Incoming',$flags).GetValue($null); $deliveryType=$delivery.GetType()
$historyType=$asm.GetType('Codepass.History')
$testRoot=if($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $PSScriptRoot ('..\build\switch-sms-fixtures-'+[Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $testRoot | Out-Null
$pass=0; $fail=0
function Assert([string]$name,[bool]$value) {
    if($value) { $script:pass++; Write-Output ('PASS '+$name) }
    else { $script:fail++; Write-Output ('FAIL '+$name) }
}
function Call($class,[string]$name,$target,[object[]]$values) {
    $parameters=New-Object object[] $values.Length
    for($i=0;$i -lt $values.Length;$i++) { if($null -ne $values[$i]) { $parameters[$i]=$values[$i].PSObject.BaseObject } }
    if($null -ne $target) { $target=$target.PSObject.BaseObject }
    $method=$class.GetMethods($flags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $values.Length } | Select-Object -First 1
    if($null -eq $method) { throw ('Missing test method '+$name) }
    return ,($method.Invoke($target,$parameters))
}
function Rejected([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
function NewConfig {
    $c=[Activator]::CreateInstance($configType,$true)
    $c.PcIp='192.0.2.1'; $c.EmailEnabled=$false; $c.QqPopupEnabled=$false
    $c.EmailAutoCopy=$false; $c.EmailNotify=$false
    return $c
}
function NewWindow($cfg,[string]$path) {
    $programType.GetField('cfg',$flags).SetValue($null,$cfg.PSObject.BaseObject)
    $programType.GetField('appDir',$flags).SetValue($null,[string]$testRoot)
    return [Activator]::CreateInstance($windowType,$flags,$null,[object[]]@($cfg.PSObject.BaseObject,$path),$null)
}
function Element($window,[string]$name) { $window.Content.FindName($name) }
function GetPrivate($window,[string]$name) { $windowType.GetField($name,$flags).GetValue($window) }
function SetPrivate($window,[string]$name,$value) { $windowType.GetField($name,$flags).SetValue($window,$value) }
function Snapshot { return ,(Call $historyType 'Snapshot' $null @()) }

$cfg=NewConfig
Assert 'config:default-500' ($cfg.SmsMaxLength -eq 500)
$cfg.SmsMaxLength=750
$roundtripPath=Join-Path $testRoot 'roundtrip.ini'; $cfg.Save($roundtripPath)
$loaded=Call $configType 'Load' $null @($roundtripPath)
Assert 'config:length-roundtrip' ($loaded.SmsMaxLength -eq 750)
$clone=NewConfig; $clone.CopyFrom($cfg)
Assert 'config:length-copy' ($clone.SmsMaxLength -eq 750)
$exportPath=Join-Path $testRoot 'export.ini'; $cfg.Save($exportPath,$false,$true)
Assert 'config:length-export' ((Call $configType 'Load' $null @($exportPath,$true)).SmsMaxLength -eq 750)
$legacyPath=Join-Path $testRoot 'legacy.ini'
[IO.File]::WriteAllText($legacyPath,"port=8787`nmin_len=4`nmax_len=8`n")
Assert 'config:legacy-default-500' ((Call $configType 'Load' $null @($legacyPath,$true)).SmsMaxLength -eq 500)
foreach($invalid in @('0','-1','10001','bad')) {
    [IO.File]::WriteAllText($legacyPath,"port=8787`nmin_len=4`nmax_len=8`nsms_max_length=$invalid`n")
    Assert ('config:invalid-strict-'+$invalid) (Rejected { Call $configType 'Load' $null @($legacyPath,$true) })
    Assert ('config:invalid-safe-default-'+$invalid) ((Call $configType 'Load' $null @($legacyPath)).SmsMaxLength -eq 500)
}
$cfg.SmsMaxLength=0
Assert 'config:invalid-save-rejected' (Rejected { $cfg.Save($roundtripPath) })
$limitMethod='SmsExceedsLengthLimit'
Assert 'length:500-accepted' (-not (Call $deliveryType $limitMethod $null @(('x'*500),500)))
Assert 'length:501-rejected' (Call $deliveryType $limitMethod $null @(('x'*501),500))
Assert 'length:chinese-characters-not-bytes' (-not (Call $deliveryType $limitMethod $null @((([string][char]0x4E2D)*500),500)))
$emoji=[char]::ConvertFromUtf32(0x1F600)
Assert 'length:emoji-one-character' (-not (Call $deliveryType $limitMethod $null @((($emoji*499)+'a'),500)))
Assert 'length:emoji-over-boundary' (Call $deliveryType $limitMethod $null @((($emoji*500)+'a'),500))
Assert 'length:spaces-and-newlines-counted' (Call $deliveryType $limitMethod $null @(('x'*499+"`r`n"),500))
Assert 'length:null-safe' (-not (Call $deliveryType $limitMethod $null @($null,500)))
Assert 'length:invalid-limit-falls-back' (Call $deliveryType $limitMethod $null @(('x'*501),0))

$smsCfg=NewConfig; $smsCfg.SmsMaxLength=500
$programType.GetField('cfg',$flags).SetValue($null,$smsCfg)
$historyPath=Join-Path $testRoot 'history.txt'
[void](Call $historyType 'Init' $null @($historyPath))
$short='verification code 123456'; $exact=$short+('x'*(500-$short.Length)); $tooLong=$exact+'x'
[void](Call $deliveryType 'HandleIncoming' $delivery @($exact,'http','Synthetic',$false,$false,$false))
Assert 'http:exact-limit-recorded' ((Snapshot).Count -eq 1)
Assert 'http:raw-not-truncated' ((Snapshot)[0].Raw.Length -eq 500)
$latest=(Call $programType 'GetLatestReceipt' $null @()).Code
[void](Call $deliveryType 'HandleIncoming' $delivery @($tooLong,'http','Synthetic',$false,$false,$false))
Assert 'http:over-limit-not-recorded' ((Snapshot).Count -eq 1)
Assert 'http:over-limit-latest-unchanged' ((Call $programType 'GetLatestReceipt' $null @()).Code -eq $latest)
[void](Call $deliveryType 'HandleIncoming' $delivery @($tooLong,'ntfy','Synthetic',$false,$false,$false))
Assert 'ntfy:over-limit-not-recorded' ((Snapshot).Count -eq 1)
$smsCfg.SmsMaxLength=600
[void](Call $deliveryType 'HandleIncoming' $delivery @($tooLong,'ntfy','Synthetic',$false,$false,$false))
Assert 'ntfy:adjusted-limit-effective' ((Snapshot).Count -eq 2)
$incoming=$deliveryType.GetMethods($flags) | Where-Object { $_.Name -eq 'HandleIncoming' -and $_.GetParameters().Count -eq 6 } | Select-Object -First 1
$lengthField=$configType.GetField('SmsMaxLength',$flags)
$saveGate=$configType.GetField('SaveGate',$flags).GetValue($null)
$smsCfg.SmsMaxLength=500
$beforeConcurrent=(Snapshot).Count
$blocked=[SmsTransactionProbe]::InvokeAcrossRollback($incoming,$delivery,$smsCfg,$lengthField,$saveGate,$tooLong,10000,500)
Assert 'concurrency:temporary-higher-limit-waits' $blocked
Assert 'concurrency:rollback-higher-limit-not-recorded' ((Snapshot).Count -eq $beforeConcurrent)
$blocked=[SmsTransactionProbe]::InvokeAcrossRollback($incoming,$delivery,$smsCfg,$lengthField,$saveGate,($exact.Replace('123456','123457')),50,500)
Assert 'concurrency:temporary-lower-limit-waits' $blocked
Assert 'concurrency:rollback-lower-limit-not-dropped' ((Snapshot).Count -eq $beforeConcurrent+1)
$smsCfg.SmsMaxLength=50
[void](Call $deliveryType 'HandleIncoming' $delivery @($tooLong,'email','synthetic@example.com',$false,$false,$true))
Assert 'email:unaffected-by-sms-limit' ((Snapshot).Count -eq $beforeConcurrent+2 -and (Snapshot)[0].Channel -eq 'email')

$uiCfg=NewConfig; $uiCfg.SmsMaxLength=500
$uiPath=Join-Path $testRoot 'ui-config.ini'; $uiCfg.Save($uiPath)
$window=NewWindow $uiCfg $uiPath
Assert 'ui:length-field-present' ($null -ne (Element $window 'txtSmsMaxLength'))
Assert 'ui:length-default-visible' ((Element $window 'txtSmsMaxLength').Text -eq '500')
[void](Call $windowType 'SelectSection' $window @('general'))
Assert 'ui:sms-card-visible' ((Element $window 'cardNetwork').Visibility.ToString() -eq 'Visible')
(Element $window 'txtSmsMaxLength').Text='650'
Assert 'ui:length-save' (Call $windowType 'ApplySettings' $window @())
Assert 'ui:length-persisted' ($uiCfg.SmsMaxLength -eq 650 -and (Call $configType 'Load' $null @($uiPath)).SmsMaxLength -eq 650)
(Element $window 'txtSmsMaxLength').Text='0'
Assert 'ui:length-invalid-rejected' (-not (Call $windowType 'ApplySettings' $window @()))
Assert 'ui:length-invalid-draft-kept' ((Element $window 'txtSmsMaxLength').Text -eq '0' -and $uiCfg.SmsMaxLength -eq 650)
(Element $window 'txtSmsMaxLength').Text='10001'
Assert 'ui:length-upper-limit-rejected' (-not (Call $windowType 'ApplySettings' $window @()))
(Element $window 'txtSmsMaxLength').Text='650'
[void](Call $windowType 'SelectSection' $window @('security'))
$privacyText=[string][char]0x9690+[char]0x79C1+[char]0x8BBE+[char]0x7F6E
Assert 'privacy:nav-renamed' ((Element $window 'navSecurity').Content -eq $privacyText)
Assert 'privacy:card-visible' ((Element $window 'cardNotify').Visibility.ToString() -eq 'Visible')
Assert 'privacy:sms-selector-present' ($null -ne (Element $window 'cmbPrivacy'))
Assert 'privacy:email-selector-present' ($null -ne (Element $window 'cmbEmailPrivacy'))
$privacyCfg=NewConfig
$privacyCfg.PrivacyMode=0; $privacyCfg.EmailPrivacyMode=0
$programType.GetField('cfg',$flags).SetValue($null,$privacyCfg.PSObject.BaseObject)
$received=[string][char]0x6536+[char]0x5230+[char]0x4E00+[char]0x6761+[char]0x65B0+[char]0x7684+[char]0x9A8C+[char]0x8BC1+[char]0x7801
Assert 'privacy:sms-default-hides-content' ((Call $programType 'ChannelNotificationText' $null @('123456','security code 123456',$false)) -eq $received)
$privacyCfg.PrivacyMode=1
Assert 'privacy:sms-mask-mode-masks-code' ((Call $programType 'ChannelNotificationText' $null @('123456','security code 123456',$false)) -eq ('security code '+'******'))
$privacyCfg.EmailPrivacyMode=1
Assert 'privacy:email-mask-mode-independent' ((Call $programType 'ChannelNotificationText' $null @('123456','security code 123456',$true)) -eq ('security code '+'******'))
$privacyCfg.EmailPrivacyMode=2
Assert 'privacy:email-full-mode-independent' ((Call $programType 'ChannelNotificationText' $null @('123456','security code 123456',$true)) -eq 'security code 123456')
$legacyPrivacy=Join-Path $testRoot 'legacy-privacy.ini'
[IO.File]::WriteAllText($legacyPrivacy,"port=8787`nmin_len=4`nmax_len=8`nprivacy_mode=1`n")
$legacyPrivacyCfg=Call $configType 'Load' $null @($legacyPrivacy,$true)
Assert 'privacy:legacy-email-follows-sms' ($legacyPrivacyCfg.EmailPrivacyMode -eq 1)
$programType.GetField('cfg',$flags).SetValue($null,$uiCfg.PSObject.BaseObject)
[void](Call $windowType 'SelectSection' $window @('email'))
$providers=Element $window 'cmbEmailProvider'
foreach($index in @(1,2,3,4,0)) {
    $providers.SelectedIndex=$index
    Assert ('switch:provider-'+$index+'-explicit-save') (Call $windowType 'ApplySettings' $window @())
    $expected=@('imap.qq.com','imap.163.com','imap.126.com','imap.gmail.com','outlook.office365.com')[$index]
    Assert ('switch:provider-'+$index+'-saved') ($uiCfg.EmailServer -eq $expected -and (Call $configType 'Load' $null @($uiPath)).EmailServer -eq $expected)
    Assert ('switch:provider-'+$index+'-no-recovery-lock') (-not (GetPrivate $window 'settingsRecoveryRequired'))
}
# Existing host credential must not be sent to a newly selected provider.
$uiCfg.EmailEnabled=$true; $uiCfg.EmailAccount='fixture@example.com'; $uiCfg.EmailAuth='synthetic-old-host'
$uiCfg.EmailAuthEncRaw='old-undecipherable-fixture'; $uiCfg.EmailAuthUndecryptable=$true
[void](Call $windowType 'LoadValues' $window @())
$providers.SelectedIndex=1
Assert 'switch:provider-draft-not-yet-applied' ($uiCfg.EmailServer -eq 'imap.qq.com')
[void](Call $windowType 'ApplySettings' $window @())
Assert 'switch:old-reception-paused' (-not $uiCfg.EmailEnabled)
Assert 'switch:old-credential-cleared' ($uiCfg.EmailAuth.Length -eq 0)
Assert 'switch:old-ciphertext-cleared' ($uiCfg.EmailAuthEncRaw.Length -eq 0 -and -not $uiCfg.EmailAuthUndecryptable)
Assert 'switch:no-old-auth-persisted' ((Call $configType 'Load' $null @($uiPath)).EmailAuth.Length -eq 0)

$baseline=[IO.File]::ReadAllBytes($uiPath)
[IO.File]::SetAttributes($uiPath,[IO.FileAttributes]::ReadOnly)
try {
    $beforeServer=$uiCfg.EmailServer; $beforeIndex=$providers.SelectedIndex
    $providers.SelectedIndex=3
    Assert 'save:readonly-save-fails' (-not (Call $windowType 'ApplySettings' $window @()))
    Assert 'save:readonly-original-preserved' ([Convert]::ToBase64String([IO.File]::ReadAllBytes($uiPath)) -eq [Convert]::ToBase64String($baseline))
    Assert 'save:readonly-memory-restored' ($uiCfg.EmailServer -eq $beforeServer)
    Assert 'save:readonly-provider-draft-kept' ($providers.SelectedIndex -eq 3)
    Assert 'save:readonly-retry-state-present' (GetPrivate $window 'settingsRecoveryRequired')
    Assert 'save:readonly-error-classified' ((Element $window 'lblSettingsFeedback').Text.Length -gt 0)
    SetPrivate $window 'settingsRecoveryRequired' $true
    Assert 'save:existing-recovery-retry-fails' (-not (Call $windowType 'ApplySettings' $window @()))
    Assert 'save:existing-recovery-kept-on-failure' (GetPrivate $window 'settingsRecoveryRequired')
} finally { [IO.File]::SetAttributes($uiPath,[IO.FileAttributes]::Normal) }
Assert 'save:recovery-retry-after-unlock-succeeds' (Call $windowType 'ApplySettings' $window @())
Assert 'save:recovery-cleared-after-unlock' (-not (GetPrivate $window 'settingsRecoveryRequired'))
$providers.SelectedIndex=3
[void](Call $windowType 'ApplySettings' $window @())
Assert 'save:retry-after-readonly-cleared' ($uiCfg.EmailServer -eq 'imap.gmail.com')
SetPrivate $window 'settingsRecoveryRequired' $true
Assert 'save:same-values-retry-recovery' (Call $windowType 'ApplySettings' $window @())
Assert 'save:same-values-clears-old-lock' (-not (GetPrivate $window 'settingsRecoveryRequired'))
$held=[IO.File]::Open($uiPath,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
try {
    $providers.SelectedIndex=2
    Assert 'save:sharing-failure-rejected' (-not (Call $windowType 'ApplySettings' $window @()))
    Assert 'save:sharing-failure-memory-restored' ($uiCfg.EmailServer -eq 'imap.gmail.com')
    Assert 'save:sharing-failure-retry-available' (GetPrivate $window 'settingsRecoveryRequired')
} finally { $held.Dispose() }
$providers.SelectedIndex=2
[void](Call $windowType 'ApplySettings' $window @())
Assert 'save:retry-after-sharing-release' ($uiCfg.EmailServer -eq 'imap.126.com')
# A persistent failure must not cancel ExitApp / tray exit.
[IO.File]::SetAttributes($uiPath,[IO.FileAttributes]::ReadOnly)
try {
    SetPrivate $window 'settingsRecoveryRequired' $true
    (Element $window 'txtSmsMaxLength').Text='700'
    $exitOk=Call $windowType 'ExitApp' $window @()
    Assert 'exit:readonly-save-failure-does-not-block' $exitOk
    Assert 'exit:closed-flag-set' (GetPrivate $window 'closeCompleted')
} finally { [IO.File]::SetAttributes($uiPath,[IO.FileAttributes]::Normal) }
Write-Output ('FIXTURES='+$testRoot)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0) { exit 1 }
