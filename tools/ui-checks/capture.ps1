param([string]$ExePath, [string]$OutDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[Windows.Media.RenderOptions]::ProcessRenderMode=[Windows.Interop.RenderMode]::SoftwareOnly
Add-Type -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location) -TypeDefinition @'
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
public static class ProbeCapture {
    public static bool Save(Visual visual, int width, int height, string path) {
        RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        PngBitmapEncoder png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream output = File.Create(path)) png.Save(output);
        return true;
    }
}
'@
$asm=[Reflection.Assembly]::LoadFile((Resolve-Path $ExePath).Path)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$configType=$asm.GetType('Codepass.Config'); $windowType=$asm.GetType('Codepass.MainWindow'); $programType=$asm.GetType('Codepass.Program')
$historyType=$asm.GetType('Codepass.History'); $itemType=$asm.GetType('Codepass.HistoryItem')
$root=Join-Path ([IO.Path]::GetFullPath($OutDirectory)) 'fixture'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cfg=[Activator]::CreateInstance($configType,$true)
$cfg.PcIp='192.168.1.20'
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
$programType.GetField('appDir',$flags).SetValue($null,$root)
$configPath=Join-Path $root 'config.ini'; $cfg.Save($configPath)
[void]$historyType.GetMethod('Init',$flags).Invoke($null,@([string](Join-Path $root 'history.json')))
$addMethod=$historyType.GetMethod('Add',$flags)
function Seed([int]$minutesAgo,[string]$source,[string]$code,[string]$raw,[string]$channel) {
    $item=[Activator]::CreateInstance($itemType,$true)
    $item.Time=[DateTime]::Now.AddMinutes(-$minutesAgo).ToString('MM-dd HH:mm:ss')
    $item.Source=$source; $item.Code=$code; $item.Raw=$raw; $item.Channel=$channel
    $item.ReceivedUtcTicks=[DateTime]::UtcNow.AddMinutes(-$minutesAgo).Ticks
    [void]$addMethod.Invoke($null,@($item.PSObject.BaseObject))
}
Seed 9 'GitHub' '384920' 'Your GitHub verification code is 384920. It expires in 15 minutes.' 'email'
Seed 7 'Google' '527143' '您正在登录 Google，验证码 527143，5 分钟内有效。' 'email'
Seed 5 'Steam' '912704' '[Steam] 您的安全代码是 912704，请勿泄露给他人。' 'sms'
Seed 3 'Microsoft' '728143' '您的 Microsoft 验证码为 728143，如非本人操作请忽略。' 'email'
Seed 1 'GitHub' '605318' '【GitHub】您的验证码是 605318，15 分钟内有效。' 'sms'
[void]$programType.GetMethod('RestoreLatestFromHistory',$flags).Invoke($null,@())
$window=$windowType.GetConstructor($flags,$null,[Type[]]@($configType,[string]),$null).Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$configPath))
$window.ShowActivated=$false
$window.Show()
$frame=New-Object Windows.Threading.DispatcherFrame
[void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame.Continue=$false })
[Windows.Threading.Dispatcher]::PushFrame($frame)
$updateMethod=$windowType.GetMethods($flags) | Where-Object { $_.Name -eq 'UpdateResponsiveForms' -and $_.GetParameters().Count -eq 0 } | Select-Object -First 1
$sectionMethod=$windowType.GetMethods($flags) | Where-Object { $_.Name -eq 'SelectSection' -and $_.GetParameters().Count -eq 1 } | Select-Object -First 1
function Layout([int]$w,[int]$h){
  $window.Width=$w; $window.Height=$h
  $window.Content.Opacity=1
  $window.Content.Measure((New-Object Windows.Size($w,$h)))
  $window.Content.Arrange((New-Object Windows.Rect(0,0,$w,$h)))
  $window.Content.UpdateLayout()
  [void]$updateMethod.Invoke($window,@())
  $window.Content.UpdateLayout()
}
function Capture([string]$name,[int]$w,[int]$h){
  Layout $w $h
  [void][ProbeCapture]::Save($window.Content,$w,$h,(Join-Path ([IO.Path]::GetFullPath($OutDirectory)) $name))
  Write-Output ('SAVED='+$name)
}
[void]$sectionMethod.Invoke($window,@('history'))
$searchBox=$window.Content.FindName('txtHistorySearch')
if($searchBox) { $searchBox.Text='' }
Capture 'history-search.png' 1100 760
foreach($section in @(@('general','sms-settings.png'),@('email','email-settings.png'),@('security','privacy-settings.png'))) {
  [void]$sectionMethod.Invoke($window,@($section[0]))
  if($section[0] -eq 'general') { $window.Content.FindName('chkTip').IsChecked=$true }
  Capture $section[1] 780 760
}
$window.Close()
