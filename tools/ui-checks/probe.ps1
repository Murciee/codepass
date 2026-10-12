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
$root=Join-Path ([IO.Path]::GetFullPath($OutDirectory)) 'fixture'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cfg=[Activator]::CreateInstance($configType,$true)
$cfg.PcIp='192.168.0.111'
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
$programType.GetField('appDir',$flags).SetValue($null,$root)
$configPath=Join-Path $root 'config.ini'; $cfg.Save($configPath)
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
foreach($section in @(@('general','sms'),@('email','email'),@('security','privacy'))) {
  [void]$sectionMethod.Invoke($window,@($section[0]))
  if($section[0] -eq 'general') { $window.Content.FindName('chkTip').IsChecked=$true }
  Capture ($section[1]+'-wide.png') 1180 900
  Capture ($section[1]+'-narrow.png') 700 900
  Capture ($section[1]+'-tall.png') 1180 1500
}
[void]$sectionMethod.Invoke($window,@('about'))
Capture 'about-wide.png' 900 900
# Revealed state of the ntfy token field (slashed eye) plus the box rect for cropping.
[void]$sectionMethod.Invoke($window,@('general'))
Layout 1180 1500
$secret=$windowType.GetField('fldNtfyToken',$flags).GetValue($window)
$toggle=$secret.GetType().GetField('toggle',$flags).GetValue($secret)
$addon=$toggle.Parent.Parent
$p=$addon.TransformToAncestor($window.Content).Transform((New-Object Windows.Point(0,0)))
Write-Output ('RECT=ntfyToken ' + [int]$p.X + ' ' + [int]$p.Y + ' ' + [int]$addon.ActualWidth + ' ' + [int]$addon.ActualHeight)
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Capture 'sms-revealed.png' 1180 1500
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
$window.Close()
