# Synthetic sockets only; never touches the running app, its port or its config.
param([string]$ExePath,[string]$FixtureDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath)); $flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$cfgType=$asm.GetType('Codepass.Config'); $programType=$asm.GetType('Codepass.Program')
$receiverType=$asm.GetType('Codepass.HttpReceiver'); $stateType=$asm.GetType('Codepass.RuntimeStatus')
$channelType=$asm.GetType('Codepass.ConnectionState'); $windowType=$asm.GetType('Codepass.MainWindow')
$historyType=$asm.GetType('Codepass.History'); $logType=$asm.GetType('Codepass.Log')
$deliveryType=$asm.GetType('Codepass.IncomingDelivery')
$root=[IO.Path]::GetFullPath($FixtureDirectory); New-Item -ItemType Directory -Path $root -Force | Out-Null
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
function HttpState(){ return $stateType.GetField('Http',$flags).GetValue($null) }
function SetHttpState([string]$name){ $stateType.GetField('Http',$flags).SetValue($null,[Enum]::Parse($channelType,$name)) }
function Wait-For([string]$name,[int]$timeoutMs){
 $want=[Enum]::Parse($channelType,$name); $clock=[Diagnostics.Stopwatch]::StartNew()
 while($clock.ElapsedMilliseconds -lt $timeoutMs){ if((HttpState).Equals($want)){return $true}; Start-Sleep -Milliseconds 100 }
 return $false
}
function NewFreePort(){
 $probe=New-Object System.Net.Sockets.TcpListener ([System.Net.IPAddress]::Loopback),0
 $probe.Start(); $free=[int]$probe.LocalEndpoint.Port; $probe.Stop(); return $free
}
function CanConnect([string]$ip,[int]$port){
 $client=New-Object System.Net.Sockets.TcpClient
 try{ $r=$client.BeginConnect($ip,$port,$null,$null)
  if(-not $r.AsyncWaitHandle.WaitOne(800)){return $false}
  $client.EndConnect($r); return $true } catch { return $false } finally { $client.Close() }
}
function Health([string]$ip,[int]$port,[string]$token){
 $client=New-Object System.Net.Sockets.TcpClient
 try{
  $client.Connect($ip,$port); $stream=$client.GetStream(); $stream.ReadTimeout=3000
  $bytes=[Text.Encoding]::ASCII.GetBytes("GET /health HTTP/1.1`r`nHost: probe`r`nX-Token: $token`r`nConnection: close`r`n`r`n")
  $stream.Write($bytes,0,$bytes.Length); $stream.Flush()
  $buffer=New-Object byte[] 256; $read=$stream.Read($buffer,0,$buffer.Length)
  return [Text.Encoding]::ASCII.GetString($buffer,0,$read)
 } finally { $client.Close() }
}

# The receiver reads its listen settings through Program.cfg; the test compiles a real CLR
# delegate with an expression tree, which is the same shape the program injects.
$cfg=[Activator]::CreateInstance($cfgType,$true)
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
$programType.GetField('appDir',$flags).SetValue($null,$root)
$logPath=Join-Path $root 'app.log'; [void](Call $logType 'Init' $null @($logPath))
$funcType=[Func``1].MakeGenericType($cfgType)
$actionType=[Action]
$configDelegate=[System.Linq.Expressions.Expression]::Lambda($funcType,[System.Linq.Expressions.Expression]::Field($null,$programType.GetField('cfg',[Reflection.BindingFlags]'NonPublic,Static'))).Compile()
$statusDelegate=[System.Delegate]::CreateDelegate($actionType,$programType.GetMethod('EmailStatusChanged',$flags))
$receiver=$receiverType.GetConstructor($flags,$null,[Type[]]@($funcType,$deliveryType,$actionType),$null).Invoke([object[]]@($configDelegate.PSObject.BaseObject,$null,$statusDelegate.PSObject.BaseObject))

Assert 'receiver:retry-policy-fast-then-capped' ((Call $receiverType 'RetryDelayMs' $null @(0)) -eq 1000 -and (Call $receiverType 'RetryDelayMs' $null @(1)) -eq 2000 -and (Call $receiverType 'RetryDelayMs' $null @(40)) -eq 30000)

# Address resolution keeps the original security rules: no token means loopback only,
# and invalid or loopback addresses are downgraded instead of exposing the port.
$cfg.Token=''; $cfg.PcIp='192.0.2.50'
Assert 'receiver:no-token-binds-loopback' ((Call $receiverType 'ResolveListenAddress' $null @($cfg)).ToString() -eq '127.0.0.1')
$cfg.Token='probe-token'; $cfg.PcIp=''
Assert 'receiver:blank-address-binds-any' ((Call $receiverType 'ResolveListenAddress' $null @($cfg)).ToString() -eq '0.0.0.0')
$cfg.PcIp='127.0.0.1'
Assert 'receiver:loopback-address-downgraded' ((Call $receiverType 'ResolveListenAddress' $null @($cfg)).ToString() -eq '127.0.0.1')
$cfg.PcIp='not-an-address'
Assert 'receiver:invalid-address-downgraded' ((Call $receiverType 'ResolveListenAddress' $null @($cfg)).ToString() -eq '127.0.0.1')
$cfg.PcIp='192.0.2.50'
Assert 'receiver:configured-address-kept' ((Call $receiverType 'ResolveListenAddress' $null @($cfg)).ToString() -eq '192.0.2.50')

# A bind that fails because the configured address is not there yet (virtual adapter comes up
# later at logon) must be retried, not written off for the rest of the session.
$port=NewFreePort
$cfg.PcIp='192.0.2.50'; $cfg.Port=$port
[void](Call $receiverType 'Start' $receiver @())
Assert 'receiver:unavailable-address-retries' (Wait-For 'Retrying' 5000)
Assert 'receiver:unavailable-address-not-bound' (-not (CanConnect '127.0.0.1' $port))
$cfg.PcIp='127.0.0.1'
Assert 'receiver:binds-when-address-arrives' (Wait-For 'Ready' 25000)
Assert 'receiver:serves-health-after-retry' ((Health '127.0.0.1' $port 'probe-token') -like '*200 OK*')
Assert 'receiver:wrong-token-still-rejected' ((Health '127.0.0.1' $port 'wrong-token') -like '*403*')
Assert 'receiver:reports-bound-endpoint' (($receiverType.GetField('BoundEndpoint',$flags).GetValue($receiver)) -eq ('127.0.0.1:' + $port))
$logText=Get-Content -LiteralPath $logPath -Raw
Assert 'receiver:logs-bind-failure-and-recovery' ($logText -match 'HTTP listen FAILED' -and $logText -match 'bind retried' -and $logText -match 'HTTP listening on')
[void](Call $receiverType 'Stop' $receiver @())
Assert 'receiver:port-released-on-stop' (-not (CanConnect '127.0.0.1' $port))

# The other failure seen in the field: a restart while the old instance still holds the port
# (address already in use). It has to recover on its own once the port is free again.
$busyPort=NewFreePort
$blocker=New-Object System.Net.Sockets.TcpListener ([System.Net.IPAddress]::Loopback),$busyPort
$blocker.Start()
$cfg.PcIp='127.0.0.1'; $cfg.Port=$busyPort
[void](Call $receiverType 'Start' $receiver @())
Assert 'receiver:busy-port-retries' (Wait-For 'Retrying' 5000)
$blocker.Stop()
Assert 'receiver:binds-when-port-freed' (Wait-For 'Ready' 25000)
Assert 'receiver:busy-port-recovery-serves-health' ((Health '127.0.0.1' $busyPort 'probe-token') -like '*200 OK*')
[void](Call $receiverType 'Stop' $receiver @())
Assert 'receiver:busy-port-released-on-stop' (-not (CanConnect '127.0.0.1' $busyPort))

# Stopping must not wait out a retry interval: a worker parked on the next retry has to end at once.
$cfg.PcIp='192.0.2.50'
[void](Call $receiverType 'Start' $receiver @())
Assert 'receiver:restart-after-stop-retries' (Wait-For 'Retrying' 5000)
$shutdown=[Diagnostics.Stopwatch]::StartNew(); [void](Call $receiverType 'Stop' $receiver @()); $shutdown.Stop()
$worker=$receiverType.GetField('thread',$flags).GetValue($receiver)
Assert 'receiver:stop-interrupts-retry-wait' ($shutdown.ElapsedMilliseconds -lt 800)
Assert 'receiver:stop-ends-worker-thread' (-not $worker.IsAlive)

# The settings page has to show the receiver state and where it listens; the log alone is not a UI.
$cfg.PcIp=''; $cfg.Port=$port; $configPath=Join-Path $root 'config.ini'; $cfg.Save($configPath)
[void](Call $historyType 'Init' $null @((Join-Path $root 'history.txt')))
$window=$windowType.GetConstructor($flags,$null,[Type[]]@($cfgType,[string]),$null).Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$configPath))
$label=$window.Content.FindName('lblPcStatus')
Assert 'status:label-present' ($null -ne $label)
$readyState=[Enum]::Parse($channelType,'Ready'); $retryState=[Enum]::Parse($channelType,'Retrying')
$boundField=$receiverType.GetField('BoundEndpoint',$flags)
$liveReceiver=$programType.GetField('Http',$flags).GetValue($null)
$boundField.SetValue($liveReceiver,'0.0.0.0:8888')
SetHttpState 'Ready'; [void](Call $windowType 'UpdateEmailStatus' $window @()); $readyText=$label.Text
Assert 'status:label-shows-bound-endpoint' ($readyText.Contains('0.0.0.0:8888'))
Assert 'status:label-follows-ready' ($readyText.Length -gt 0 -and $readyText.Contains([string](Call $stateType 'ConnectionText' $null @($readyState))))
Assert 'status:label-quiet-when-ready' ($label.Foreground -ne [Windows.Media.Brushes]::Firebrick)
SetHttpState 'Retrying'; [void](Call $windowType 'UpdateEmailStatus' $window @()); $retryText=$label.Text
Assert 'status:label-follows-retry' ($retryText.Length -gt 0 -and $retryText -ne $readyText -and $retryText.Contains([string](Call $stateType 'ConnectionText' $null @($retryState))))
Assert 'status:label-warns-on-retry' ($label.Foreground -eq [Windows.Media.Brushes]::Firebrick)
SetHttpState 'Ready'; [void](Call $windowType 'UpdateEmailStatus' $window @())
Assert 'status:label-clears-warning-when-recovered' ($label.Foreground -ne [Windows.Media.Brushes]::Firebrick)
# A configured LAN address that was downgraded to loopback cannot be reached from the phone and
# must not be reported as a healthy receiver; without a token, loopback is the intended setup.
$cfg.Token='probe-token'; $cfg.PcIp='192.0.2.50'; $boundField.SetValue($liveReceiver,'127.0.0.1:8888')
SetHttpState 'Ready'; [void](Call $windowType 'UpdateEmailStatus' $window @())
Assert 'status:label-flags-loopback-downgrade' ($label.Text -ne $readyText -and $label.Foreground -eq [Windows.Media.Brushes]::Firebrick)
$cfg.Token=''; $boundField.SetValue($liveReceiver,'0.0.0.0:8888')
SetHttpState 'Ready'; [void](Call $windowType 'UpdateEmailStatus' $window @())
Assert 'status:label-allows-loopback-without-token' ($label.Text -eq $readyText -and $label.Foreground -ne [Windows.Media.Brushes]::Firebrick)

Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0){exit 1}
