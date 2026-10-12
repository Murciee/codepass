param([string]$ExePath, [string]$OutDirectory)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[Windows.Media.RenderOptions]::ProcessRenderMode=[Windows.Interop.RenderMode]::SoftwareOnly
$asm=[Reflection.Assembly]::LoadFile((Resolve-Path $ExePath).Path)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$configType=$asm.GetType('Codepass.Config'); $windowType=$asm.GetType('Codepass.MainWindow'); $programType=$asm.GetType('Codepass.Program')
$root=Join-Path ([IO.Path]::GetFullPath($OutDirectory)) 'fixture'
New-Item -ItemType Directory -Force -Path $root | Out-Null
$cfg=[Activator]::CreateInstance($configType,$true)
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
$content=$window.Content
function Layout([int]$w,[int]$h){
  $window.Width=$w; $window.Height=$h
  $content.Opacity=1
  $content.Measure((New-Object Windows.Size($w,$h)))
  $content.Arrange((New-Object Windows.Rect(0,0,$w,$h)))
  $content.UpdateLayout()
  [void]$updateMethod.Invoke($window,@())
  $content.UpdateLayout()
}
function Rect([string]$label,$element,$viewport){
  $p=$element.TranslatePoint((New-Object Windows.Point(0,0)),$viewport)
  Write-Output ('RECT ' + $label + ' x=' + [Math]::Round($p.X,2) + ' y=' + [Math]::Round($p.Y,2) + ' w=' + [Math]::Round($element.ActualWidth,2) + ' h=' + [Math]::Round($element.ActualHeight,2))
}
function CardOf($element){
  $node=$element
  while($node -ne $null) {
    $grid=$node -as [Windows.Controls.Grid]
    if($grid -ne $null -and ($grid.Tag -eq 'SettingsCard' -or $grid.Tag -eq 'SettingsCardStacked')) { return $grid }
    $node=[Windows.LogicalTreeHelper]::GetParent($node)
  }
  return $null
}
[void]$sectionMethod.Invoke($window,@('email'))
Layout 1180 1500
function Probe([string]$name,[string]$elementName) {
  try {
    $element=$content.FindName($elementName)
    $card=CardOf $element
    if($card -eq $null) { Write-Output ('PROBE-FAIL ' + $name); return }
    $box=[Windows.LogicalTreeHelper]::GetParent($card)
    Rect ($name+'-box') $box $content
    $list=New-Object 'System.Collections.Generic.List[object]'
    foreach($child in [Windows.LogicalTreeHelper]::GetChildren($card)) { if($child -is [Windows.FrameworkElement]) { $list.Add($child) } }
    if($list.Count -ge 2) {
      Rect ($name+'-left') $list[0] $content
      Rect ($name+'-right') $list[1] $content
      $lp=$list[0].TranslatePoint((New-Object Windows.Point(0,0)),$card)
      $rp=$list[1].TranslatePoint((New-Object Windows.Point(0,0)),$card)
      Write-Output ('LOCAL ' + $name + ' cardH=' + [Math]::Round($card.ActualHeight,2) + ' leftTop=' + [Math]::Round($lp.Y,2) + ' leftH=' + [Math]::Round($list[0].ActualHeight,2) + ' rightTop=' + [Math]::Round($rp.Y,2) + ' rightH=' + [Math]::Round($list[1].ActualHeight,2))
    } else { Write-Output ('PROBE-KIDS ' + $name + ' count=' + $list.Count) }
  } catch { Write-Output ('PROBE-ERR ' + $name + ' : ' + $_.Exception.Message) }
}
Probe 'emailTest' 'btnEmailTest'
Probe 'emailJunk' 'chkEmailJunk'
# Secret field geometry before/after the reveal click.
$secret=$windowType.GetField('fldEmailAuth',$flags).GetValue($window)
$secretType=$secret.GetType()
$addon=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($secretType.GetField('masked',$flags).GetValue($secret))))
$toggle=$secretType.GetField('toggle',$flags).GetValue($secret)
$masked=$secretType.GetField('masked',$flags).GetValue($secret)
$plain=$secretType.GetField('plain',$flags).GetValue($secret)
$masked.Password='123456'
Rect 'secret-addon-before' $addon $content
Rect 'secret-masked-before' $masked $content
Rect 'secret-toggle-before' $toggle $content
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
$content.UpdateLayout()
Rect 'secret-addon-after' $addon $content
Rect 'secret-plain-after' $plain $content
Rect 'secret-toggle-after' $toggle $content
Rect 'secret-addon-check' $addon $content
# Lock overlay and set-password dialog snapshots.
Add-Type -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location) -TypeDefinition @'
using System.IO; using System.Windows; using System.Windows.Media; using System.Windows.Media.Imaging;
public static class Shot {
  public static void Save(Visual v, int w, int h, string path) {
    RenderTargetBitmap b = new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);
    b.Render(v);
    PngBitmapEncoder e = new PngBitmapEncoder(); e.Frames.Add(BitmapFrame.Create(b));
    using (FileStream f = File.Create(path)) e.Save(f);
  }
}
'@
[void]$sectionMethod.Invoke($window,@('security'))
Layout 1000 800
$overlay=$content.FindName('lockOverlay')
$overlay.Visibility=[Windows.Visibility]::Visible
$overlay.UpdateLayout()
[void][Shot]::Save($content,1000,800,(Join-Path ([IO.Path]::GetFullPath($OutDirectory)) 'lockoverlay.png'))
Write-Output 'SAVED=lockoverlay.png'
$dialogType=$asm.GetType('Codepass.PasswordSettingsDialog')
$dlgArgs=New-Object 'object[]' 4
$dlgArgs[0]=[Windows.Window]$window
$dlgArgs[1]=$cfg.PSObject.BaseObject
$dlgArgs[2]=[string](Join-Path $root 'config.ini')
$dlgArgs[3]=[bool]$false
$dlg=$dialogType.GetConstructor($flags,$null,[Type[]]@([Windows.Window],$configType,[string],[bool]),$null).Invoke($dlgArgs)
$dlg.ShowActivated=$false
$dlg.Show()
$frame2=New-Object Windows.Threading.DispatcherFrame
[void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame2.Continue=$false })
[Windows.Threading.Dispatcher]::PushFrame($frame2)
Write-Output ('DIALOG size=' + [int]$dlg.ActualWidth + 'x' + [int]$dlg.ActualHeight)
[void][Shot]::Save($dlg.Content,[int]$dlg.ActualWidth,[int]$dlg.ActualHeight,(Join-Path ([IO.Path]::GetFullPath($OutDirectory)) 'lock-settings-dialog.png'))
Write-Output 'SAVED=lock-settings-dialog.png'
# Reveal the second field to capture the closed-eye state.
$newPassword=$dialogType.GetField('newPassword',$flags).GetValue($dlg)
$newPassword.Password='synthetic-test-password'
$panel=$dlg.Content
$addon=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($newPassword)))
$toggle=$null
if($addon -is [Windows.Controls.Border]) {
  foreach($child in [Windows.LogicalTreeHelper]::GetChildren($addon)) {
    foreach($sub in [Windows.LogicalTreeHelper]::GetChildren($child)) {
      if($sub -is [Windows.Controls.Button]) { $toggle=$sub }
    }
  }
}
if($toggle -ne $null) {
  $toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
  $dlg.UpdateLayout()
  [void][Shot]::Save($dlg.Content,[int]$dlg.ActualWidth,[int]$dlg.ActualHeight,(Join-Path ([IO.Path]::GetFullPath($OutDirectory)) 'lock-settings-dialog-revealed.png'))
  Write-Output 'SAVED=lock-settings-dialog-revealed.png'
} else { Write-Output 'DIALOG toggle not found' }
$dlg.Close()
$window.Close()
