# Idle working-set trim and window appearance invariants. Synthetic fixtures only.
param([string]$ExePath = (Join-Path $PSScriptRoot '..\dist\codepass.exe'), [string]$FixtureDirectory = '')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$root=if($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $PSScriptRoot ('..\build\idle-memory-fixtures-'+[Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Force -Path $root | Out-Null
$logPath=Join-Path $root 'app.log'
$pass=0; $fail=0
function Assert([string]$name,[bool]$ok) { if($ok) { $script:pass++; Write-Output ('PASS '+$name) } else { $script:fail++; Write-Output ('FAIL '+$name) } }
function Call($class,[string]$name,$target,[object[]]$values) {
    $args=New-Object object[] $values.Length
    for($i=0;$i -lt $values.Length;$i++) { if($null -ne $values[$i]) { $args[$i]=$values[$i].PSObject.BaseObject } }
    if($null -ne $target) { $target=$target.PSObject.BaseObject }
    $method=$class.GetMethods($flags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $values.Length } | Select-Object -First 1
    if($null -eq $method) { throw ('Missing '+$name) }
    return ,($method.Invoke($target,$args))
}
function Pump {
    $frame=New-Object Windows.Threading.DispatcherFrame
    [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame.Continue=$false })
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}
$trimType=$asm.GetType('Codepass.MemoryTrim'); $shapeType=$asm.GetType('Codepass.WindowShape')
$configType=$asm.GetType('Codepass.Config'); $windowType=$asm.GetType('Codepass.MainWindow')
$programType=$asm.GetType('Codepass.Program'); $historyType=$asm.GetType('Codepass.History')
$logType=$asm.GetType('Codepass.Log')
$hiddenField=$trimType.GetField('hiddenSinceTicks',$flags)
$activityField=$trimType.GetField('lastActivityTicks',$flags)
$lastTrimField=$trimType.GetField('lastTrimTicks',$flags)
$timerField=$trimType.GetField('timer',$flags)
$tickMethod=$trimType.GetMethod('Tick',$flags)
$countProperty=$trimType.GetProperty('TrimCount',$flags)
function TrimCount { return [int]$countProperty.GetValue($null,$null) }
function SetHidden([long]$ticks) { $hiddenField.SetValue($null,$ticks) }
function Hidden { return [long]$hiddenField.GetValue($null) }
function TrimLogLines {
    if(-not (Test-Path $logPath)) { return 0 }
    return ([regex]::Matches([IO.File]::ReadAllText($logPath),'idle trim:')).Count
}
$now=[DateTime]::UtcNow
[void](Call $logType 'Init' $null @($logPath))

# The trim only fires on a process working set above the production threshold; keep
# this one above it so the fire/skip decisions below are deterministic.
$ballast=New-Object byte[] (48MB)
function TouchBallast([int]$round) { for($i=0;$i -lt $ballast.Length;$i+=4096) { $script:ballast[$i]=[byte]$round } }
TouchBallast 1

# --- decision logic ---
SetHidden 0; $activityField.SetValue($null,[long]0); $lastTrimField.SetValue($null,[long]0)
[void](Call $trimType 'SetWindowVisible' $null @($true))
$before=TrimCount
$tickMethod.Invoke($null,$null)
Assert 'trim:visible-window-never-trims' ((TrimCount) -eq $before)

[void](Call $trimType 'SetWindowVisible' $null @($false))
$before=TrimCount
$tickMethod.Invoke($null,$null)
Assert 'trim:grace-period-after-hide' ((TrimCount) -eq $before -and (Hidden) -ne 0)

SetHidden $now.AddSeconds(-21).Ticks
[void](Call $trimType 'PokeActivity' $null @())
$lastTrimField.SetValue($null,[long]0)
$before=TrimCount
$tickMethod.Invoke($null,$null)
Assert 'trim:settle-after-message-activity' ((TrimCount) -eq $before)

SetHidden $now.AddSeconds(-21).Ticks
$activityField.SetValue($null,$now.AddSeconds(-60).Ticks)
$lastTrimField.SetValue($null,$now.Ticks)
$before=TrimCount
$tickMethod.Invoke($null,$null)
Assert 'trim:min-interval-between-trims' ((TrimCount) -eq $before)

SetHidden $now.AddSeconds(-21).Ticks
$activityField.SetValue($null,$now.AddSeconds(-60).Ticks)
$lastTrimField.SetValue($null,[long]0)
$workingSetBefore=[Environment]::WorkingSet
$before=TrimCount
$tickMethod.Invoke($null,$null)
$workingSetAfter=[Environment]::WorkingSet
Assert 'trim:fires-when-hidden-and-idle' ((TrimCount) -eq ($before+1))
Assert 'trim:working-set-actually-shrinks' ($workingSetAfter -lt $workingSetBefore)
Assert 'trim:first-trim-logs-once' ((TrimLogLines) -eq 1)

# Below the threshold there is nothing to hand back.
$belowThreshold=[Environment]::WorkingSet -lt 32MB
SetHidden $now.AddSeconds(-21).Ticks
$activityField.SetValue($null,$now.AddSeconds(-60).Ticks)
$lastTrimField.SetValue($null,[long]0)
$before=TrimCount
$tickMethod.Invoke($null,$null)
Assert 'trim:post-trim-working-set-is-small' $belowThreshold
Assert 'trim:skips-below-threshold' ((TrimCount) -eq $before)

# One log line per hidden period: a second trim in the same period stays silent,
# a new hidden period logs again.
[void](Call $trimType 'SetWindowVisible' $null @($true))
[void](Call $trimType 'SetWindowVisible' $null @($false))
SetHidden $now.AddSeconds(-21).Ticks
$activityField.SetValue($null,$now.AddSeconds(-60).Ticks)
$lastTrimField.SetValue($null,[long]0)
TouchBallast 2
$before=TrimCount
$tickMethod.Invoke($null,$null)
$secondTrim=((TrimCount) -eq ($before+1))
TouchBallast 3
$lastTrimField.SetValue($null,[long]0)
$tickMethod.Invoke($null,$null)
Assert 'trim:second-trim-in-period-silent' ($secondTrim -and (TrimCount) -eq ($before+2) -and (TrimLogLines) -eq 2)
[void](Call $trimType 'SetWindowVisible' $null @($true))
[void](Call $trimType 'SetWindowVisible' $null @($false))
SetHidden $now.AddSeconds(-21).Ticks
$activityField.SetValue($null,$now.AddSeconds(-60).Ticks)
$lastTrimField.SetValue($null,[long]0)
TouchBallast 4
$tickMethod.Invoke($null,$null)
Assert 'trim:new-hidden-period-logs-again' ((TrimCount) -eq ($before+3) -and (TrimLogLines) -eq 3)

[void](Call $trimType 'SetWindowVisible' $null @($true))
$before=TrimCount
$tickMethod.Invoke($null,$null)
Assert 'trim:showing-window-cancels' ((TrimCount) -eq $before -and (Hidden) -eq 0)

# --- delivery wiring: an incoming message pokes activity ---
$cfg=[Activator]::CreateInstance($configType,$true)
$cfg.PcIp='192.0.2.62'; $cfg.Dry=$true
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
$programType.GetField('appDir',$flags).SetValue($null,$root)
[void](Call $historyType 'Init' $null @((Join-Path $root 'history.txt')))
$activityField.SetValue($null,[long]0)
$delivery=$programType.GetField('Incoming',$flags).GetValue($null)
[void](Call $delivery.GetType() 'ReceiveSms' $delivery @('ExampleShop security code 556677','synthetic','ExampleShop','poke-fixture',$now,$false,$false,$null))
Assert 'trim:incoming-message-pokes-activity' ([long]$activityField.GetValue($null) -ne 0)

# --- window wiring and the mica/unlayered invariant ---
$configPath=Join-Path $root 'config.ini'; $cfg.Dry=$false; $cfg.Save($configPath)
$window=$windowType.GetConstructor($flags,$null,[Type[]]@($configType,[string]),$null).Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$configPath))
$window.ShowActivated=$false
[void](Call $windowType 'ApplyConfiguredWindowSize' $window @())
$timerBefore=$timerField.GetValue($null)
[void](Call $trimType 'EnsureStarted' $null @())
Assert 'trim:timer-started-with-window' ($null -ne $timerBefore)
Assert 'trim:ensure-started-idempotent' ([object]::ReferenceEquals($timerBefore,$timerField.GetValue($null)))
Assert 'trim:constructed-window-counts-as-hidden' ((Hidden) -ne 0)
$window.Show(); Pump
Assert 'trim:shown-window-clears-hidden-state' ((Hidden) -eq 0)
$window.Hide(); Pump
Assert 'trim:hidden-window-restarts-timer' ((Hidden) -ne 0)
$window.Show(); Pump

Assert 'window:stays-non-layered' (-not $window.AllowsTransparency)
$build=[Environment]::OSVersion.Version.Build
$transparency=(Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize' -ErrorAction SilentlyContinue).EnableTransparency
if($null -eq $transparency) { $transparency=1 }
$expectMica=($build -ge 22621) -and ($transparency -ne 0) -and (-not [Windows.SystemParameters]::HighContrast)
$micaEnabled=[bool]$shapeType.GetProperty('MicaEnabled',$flags).GetValue($null,$null)
Assert 'mica:matches-os-support' ($micaEnabled -eq $expectMica)
$shell=$window.Content.FindName('shell'); $navRail=$window.Content.FindName('navRail')
Assert 'mica:shell-and-navrail-present' ($null -ne $shell -and $null -ne $navRail)
function IsTransparent($brush) { return ($brush -is [Windows.Media.SolidColorBrush]) -and ($brush.Color.A -eq 0) }
function IsOpaqueBody($brush) { return ($brush -is [Windows.Media.SolidColorBrush]) -and ($brush.Color -eq [Windows.Media.Color]::FromRgb(0xF3,0xF3,0xF3)) }
if($micaEnabled) {
    Assert 'mica:backgrounds-transparent-when-enabled' ((IsTransparent $window.Background) -and (IsTransparent $window.Content.Background) -and (IsTransparent $shell.Background) -and (IsTransparent $navRail.Background) -and (IsTransparent $shell.BorderBrush))
} else {
    Assert 'mica:backgrounds-opaque-when-unsupported' ((IsOpaqueBody $window.Background) -and (IsOpaqueBody $window.Content.Background) -and (IsOpaqueBody $shell.Background) -and (IsOpaqueBody $navRail.Background) -and ($shell.BorderBrush.Color -eq [Windows.Media.Color]::FromArgb(0x0F,0,0,0)))
}

[void](Call $windowType 'ExitApp' $window @())
Write-Output ('FIXTURES='+$root)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0) { exit 1 }
