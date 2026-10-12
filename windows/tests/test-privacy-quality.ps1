# Synthetic privacy, version and storage checks only; no live mail or runtime data.
param([string]$ExePath = (Join-Path $PSScriptRoot '..\dist\codepass.exe'), [string]$FixtureDirectory = '')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$configType=$asm.GetType('Codepass.Config'); $programType=$asm.GetType('Codepass.Program'); $windowType=$asm.GetType('Codepass.MainWindow')
$delivery=$programType.GetField('Incoming',$flags).GetValue($null); $deliveryType=$delivery.GetType()
$root=if($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $PSScriptRoot ('..\build\privacy-quality-fixtures-'+[Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Force -Path $root | Out-Null
$pass=0; $fail=0
function Assert([string]$name,[bool]$ok) { if($ok) { $script:pass++; Write-Output ('PASS '+$name) } else { $script:fail++; Write-Output ('FAIL '+$name) } }
function Rejected([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
function Call($class,[string]$name,$target,[object[]]$values) {
    $args=New-Object object[] $values.Length
    for($i=0;$i -lt $values.Length;$i++) { if($null -ne $values[$i]) { $args[$i]=$values[$i].PSObject.BaseObject } }
    if($null -ne $target) { $target=$target.PSObject.BaseObject }
    $method=$class.GetMethods($flags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $values.Length } | Select-Object -First 1
    if($null -eq $method) { throw ('Missing method '+$name) }
    return ,($method.Invoke($target,$args))
}
$cfg=[Activator]::CreateInstance($configType,$true)
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
Assert 'privacy:config-default-sms-mode' ($cfg.PrivacyMode -eq 0)
Assert 'privacy:config-default-email-mode' ($cfg.EmailPrivacyMode -eq 0)
$cfg.PrivacyMode=0
$received=[string][char]0x6536+[char]0x5230+[char]0x4E00+[char]0x6761+[char]0x65B0+[char]0x7684+[char]0x9A8C+[char]0x8BC1+[char]0x7801
Assert 'privacy:notification-full-sms' ((Call $programType 'ChannelNotificationText' $null @('123456','code 123456',$false)) -eq $received)
$cfg.PrivacyMode=1
Assert 'privacy:notification-mask-sms' ((Call $programType 'ChannelNotificationText' $null @('123456','code 123456',$false)) -eq 'code ******')
$extractor=$asm.GetType('Codepass.CodeExtractor')
foreach($json in @('{"code":"12345\u0036"}','{"code":"\u0031\u0032\u0033\u0034\u0035\u0036"}','{"code":"123456","duplicate":"12345\u0036"}','[{"code":"12345\u0036","duplicate":"123456"}]','prefix: {"code":"12345\u0036","duplicate":"123456"}')) {
    $jsonCode=Call $extractor 'Extract' $null @($json,4,8)
    Assert ('privacy:json-code-decoded-'+$pass) ($jsonCode -eq '123456')
    $safeText=Call $programType 'ChannelNotificationText' $null @($jsonCode,$json,$false)
    Assert ('privacy:json-content-falls-back-'+$pass) (-not $safeText.Contains('123') -and -not $safeText.Contains('\u003'))
}
$cfg.EmailPrivacyMode=2
Assert 'privacy:notification-full-email' ((Call $programType 'ChannelNotificationText' $null @('123456','code 123456',$true)) -eq 'code 123456')
$cfg.EmailPrivacyMode=1
Assert 'privacy:email-mask-before-cut' (-not (Call $programType 'ChannelNotificationText' $null @('123456',(('x'*116)+'123456'),$true)).Contains('1234'))
$cfg.EmailPrivacyMode=0
Assert 'privacy:email-only-received' ((Call $programType 'ChannelNotificationText' $null @('123456','code 123456',$true)) -eq $received)
$cfg.EmailPrivacyMode=2
$path=Join-Path $root 'config.ini'; $cfg.Save($path)
$loaded=Call $configType 'Load' $null @($path,$true)
Assert 'privacy:roundtrip-email-mode' ($loaded.EmailPrivacyMode -eq 2)
$legacy=Join-Path $root 'legacy.ini'; [IO.File]::WriteAllText($legacy,"port=8787`nmin_len=4`nmax_len=8`nprivacy_mode=1`n")
Assert 'privacy:legacy-mode-migrates' ((Call $configType 'Load' $null @($legacy,$true)).EmailPrivacyMode -eq 1)
[IO.File]::WriteAllText($legacy,"port=8787`nmin_len=4`nmax_len=8`nemail_privacy_mode=3`n")
Assert 'privacy:invalid-import-rejected' (Rejected { Call $configType 'Load' $null @($legacy,$true) })
$cfg.EmailPrivacyMode=3
Assert 'privacy:invalid-save-rejected' (Rejected { $cfg.Save($path) })
$cfg.EmailPrivacyMode=2; $cfg.Save($path)
[IO.File]::SetAttributes($path,[IO.FileAttributes]::ReadOnly)
try {
    Assert 'storage:readonly-save-rejected' (Rejected { $cfg.Save($path) })
    Assert 'storage:config-error-visible' ($configType.GetProperty('LastSaveError',$flags).GetValue($null,$null).Length -gt 0)
    $runtimeError=$configType.GetProperty('LastSaveError',$flags).GetValue($null,$null)
    $cfg.Save((Join-Path $root 'export.ini'),$false,$true)
    Assert 'storage:export-success-preserves-runtime-error' ($configType.GetProperty('LastSaveError',$flags).GetValue($null,$null) -eq $runtimeError)
    Assert 'storage:export-failure-rejected' (Rejected { $cfg.Save((Join-Path $root 'missing-parent\export.ini'),$false,$true) })
    Assert 'storage:export-failure-preserves-runtime-error' ($configType.GetProperty('LastSaveError',$flags).GetValue($null,$null) -eq $runtimeError)
} finally { [IO.File]::SetAttributes($path,[IO.FileAttributes]::Normal) }
$cfg.Save($path)
Assert 'storage:error-cleared-after-success' ($null -eq $configType.GetProperty('LastSaveError',$flags).GetValue($null,$null))
Assert 'storage:export-only-failure-rejected' (Rejected { $cfg.Save((Join-Path $root 'missing-parent\export.ini'),$false,$true) })
Assert 'storage:export-only-failure-does-not-set-runtime-error' ($null -eq $configType.GetProperty('LastSaveError',$flags).GetValue($null,$null))
$history=$asm.GetType('Codepass.History'); $itemType=$asm.GetType('Codepass.HistoryItem')
$programType.GetField('appDir',$flags).SetValue($null,[string]$root)
$historyPath=Join-Path $root 'history.txt'
[void](Call $history 'Init' $null @($historyPath))
$cfg.PrivacyMode=2; $cfg.EmailPrivacyMode=0; $cfg.EmailNotify=$false; $cfg.EmailAutoCopy=$false
[void](Call $deliveryType 'SubmitEmail' $delivery @('verification code 123456','synthetic@example.com','123456','privacy-latest-mail',$cfg,$false,[Func[bool]]{return $true},$null,[long]0,$true))
$latest=Call $programType 'GetLatestReceipt' $null @()
Assert 'privacy:latest-receipt-keeps-mail-channel' ($latest.Code -eq '123456' -and $latest.EmailChannel)
for($i=0;$i -lt 101;$i++) {
    $item=[Activator]::CreateInstance($itemType,$true)
    $item.Channel='email'; $item.Code='654321'; $item.CanBeLatest=$false
    [void](Call $history 'AddUnique' $null @($item))
}
Assert 'privacy:backfill-evicts-visible-latest' ($null -eq (Call $history 'LatestCode' $null @()))
$latest=Call $programType 'GetLatestReceipt' $null @()
Assert 'privacy:eviction-retains-code-and-channel' ($latest.Code -eq '123456' -and $latest.EmailChannel)
Assert 'privacy:tray-receipt-applies-mail-mode' ((Call $programType 'ChannelNotificationText' $null @($latest.Code,$latest.Code,$latest.EmailChannel)) -eq $received)
[IO.File]::SetAttributes($historyPath,[IO.FileAttributes]::ReadOnly)
try {
    Assert 'privacy:failed-clear-rejected' (Rejected { Call $history 'Clear' $null @() })
    Assert 'privacy:failed-clear-keeps-latest-receipt' ((Call $programType 'GetLatestReceipt' $null @()).EmailChannel)
} finally { [IO.File]::SetAttributes($historyPath,[IO.FileAttributes]::Normal) }
[void](Call $history 'Clear' $null @())
Assert 'privacy:successful-clear-clears-latest-receipt' ($null -eq (Call $programType 'GetLatestReceipt' $null @()))
[void](Call $deliveryType 'HandleIncoming' $delivery @('verification code 654321','http','Synthetic',$false,$false,$false))
Assert 'privacy:sms-receipt-keeps-sms-channel' (-not (Call $programType 'GetLatestReceipt' $null @()).EmailChannel)
[void](Call $deliveryType 'SubmitEmail' $delivery @('verification code 123456','synthetic@example.com','123456','privacy-restore-mail',$cfg,$false,[Func[bool]]{return $true},$null,[long]0,$true))
[void](Call $programType 'SetLatestCode' $null @($null,$false))
[void](Call $programType 'RestoreLatestFromHistory' $null @())
Assert 'privacy:restart-restores-mail-code-with-channel' ((Call $programType 'GetLatestReceipt' $null @()).Code -eq '123456' -and (Call $programType 'GetLatestReceipt' $null @()).EmailChannel)
$version=[Diagnostics.FileVersionInfo]::GetVersionInfo([IO.Path]::GetFullPath($ExePath))
$expectedVersion=$asm.GetType('Codepass.Updater').GetField('AppVersion',$flags).GetRawConstantValue()
Assert 'build:version-stamp-present' ($version.ProductVersion.Length -gt 0 -and $version.FileVersion -eq ($expectedVersion+'.0'))
$metaPath=Join-Path (Split-Path -Parent $ExePath) 'build-info.json'
Assert 'build:metadata-present' (Test-Path -LiteralPath $metaPath -PathType Leaf)
$meta=Get-Content -LiteralPath $metaPath -Raw -Encoding UTF8 | ConvertFrom-Json
Assert 'build:metadata-exe-hash-present' ($meta.exeSha256 -match '^[0-9A-Fa-f]{64}$')
Assert 'build:metadata-version-matches-product' ($meta.version -eq $expectedVersion)
$tempRoot=Join-Path $root 'stale-temp'
New-Item -ItemType Directory -Path $tempRoot | Out-Null
$oldConfig=Join-Path $tempRoot ('codepass-config.ini.'+[Guid]::NewGuid().ToString('N')+'.tmp')
$oldHistory=Join-Path $tempRoot ('history.txt.'+[Guid]::NewGuid().ToString('N')+'.tmp')
$freshConfig=Join-Path $tempRoot ('codepass-config.ini.'+[Guid]::NewGuid().ToString('N')+'.tmp')
$unrelated=Join-Path $tempRoot 'unrelated.tmp'
foreach($temporary in @($oldConfig,$oldHistory,$freshConfig,$unrelated)) { [IO.File]::WriteAllText($temporary,'synthetic fixture') }
foreach($temporary in @($oldConfig,$oldHistory,$unrelated)) { [IO.File]::SetLastWriteTimeUtc($temporary,[DateTime]::UtcNow.AddDays(-2)) }
$recycled=Call $configType 'RecycleStaleTemporaryFiles' $null @($tempRoot)
Assert 'storage:two-old-owned-temporaries-recycled' ($recycled -eq 2 -and -not (Test-Path -LiteralPath $oldConfig) -and -not (Test-Path -LiteralPath $oldHistory))
Assert 'storage:fresh-owned-temporary-retained' (Test-Path -LiteralPath $freshConfig)
Assert 'storage:unrelated-old-temporary-retained' (Test-Path -LiteralPath $unrelated)
$ctor=$windowType.GetConstructor($flags,$null,[Type[]]@($configType,[string]),$null)
$window=$ctor.Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$path))
$window.Content.Opacity=1
$privacyText=[string][char]0x9690+[char]0x79C1+[char]0x8BBE+[char]0x7F6E
Assert 'layout:privacy-nav-visible' ($window.Content.FindName('navSecurity').Content -eq $privacyText)
[void](Call $windowType 'SelectSection' $window @('security'))
Assert 'layout:privacy-card-visible' ($window.Content.FindName('cardNotify').Visibility.ToString() -eq 'Visible')
Assert 'layout:password-card-visible' ($window.Content.FindName('cardSecurity').Visibility.ToString() -eq 'Visible')
$cfg.EmailPrivacyMode=1
[void](Call $windowType 'LoadValues' $window @())
Assert 'privacy:email-selector-loads-saved-mode' ($window.Content.FindName('cmbEmailPrivacy').SelectedIndex -eq 1)
Assert 'status:email-status-lines-removed' ($null -eq $window.Content.FindName('lblEmailFolders') -and $null -eq $window.Content.FindName('lblEmailProcessing') -and $null -eq $window.Content.FindName('lblEmailStatus'))
Assert 'status:test-has-own-result-line' ($null -ne $window.Content.FindName('lblEmailTestResult'))
Assert 'storage:banner-outside-email-card' (-not [Windows.LogicalTreeHelper]::GetParent($window.Content.FindName('storageErrorBanner')).Equals($window.Content.FindName('cardEmail').Child))
Assert 'qq:disabled-without-configured-mailbox' (-not $window.Content.FindName('chkQqPopup').IsEnabled)
[void](Call $windowType 'ExitApp' $window @())
Write-Output ('FIXTURES='+$root)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0) { exit 1 }
