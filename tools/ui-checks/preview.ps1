param([string]$ExePath,[string]$OutDirectory)
$ErrorActionPreference='Stop'
[void](New-Item -ItemType Directory -Force -Path $OutDirectory)
. (Join-Path $PSScriptRoot 'probe.ps1') -ExePath $ExePath -OutDirectory $OutDirectory
function Pump {
    $f=New-Object Windows.Threading.DispatcherFrame
    [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{$f.Continue=$false})
    [Windows.Threading.Dispatcher]::PushFrame($f)
}
function Card($control) {
    $node=$control
    while($null -ne $node) {
        if($node -is [Windows.Controls.Grid] -and $node.Tag -eq 'SettingsCard') { return $node.Parent }
        $node=[Windows.LogicalTreeHelper]::GetParent($node)
    }
}
function SaveVisual($visual,[string]$name,[double]$scale=1) {
    $width=[int][Math]::Ceiling($visual.ActualWidth*$scale)
    $height=[int][Math]::Ceiling($visual.ActualHeight*$scale)
    $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap($width,$height,(96*$scale),(96*$scale),[Windows.Media.PixelFormats]::Pbgra32)
    $drawing=New-Object Windows.Media.DrawingVisual
    $context=$drawing.RenderOpen()
    $brush=New-Object Windows.Media.VisualBrush($visual)
    $context.DrawRectangle($brush,$null,(New-Object Windows.Rect(0,0,$visual.ActualWidth,$visual.ActualHeight)))
    $context.Close()
    $bitmap.Render($drawing)
    $png=New-Object Windows.Media.Imaging.PngBitmapEncoder
    $png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $file=[IO.File]::Create((Join-Path $OutDirectory $name))
    try{$png.Save($file)}finally{$file.Dispose()}
    'SAVED='+$name
}
$window.Show(); Pump
[void]$sectionMethod.Invoke($window,@('general')); Layout 1180 1500; Pump
$phoneCard=Card ($window.Content.FindName('btnGen'))
SaveVisual $phoneCard.Parent 'phone-cards.png'
$secret=$windowType.GetField('fldNtfyToken',$flags).GetValue($window)
$masked=$secret.GetType().GetField('masked',$flags).GetValue($secret)
$plain=$secret.GetType().GetField('plain',$flags).GetValue($secret)
$toggle=$secret.GetType().GetField('toggle',$flags).GetValue($secret)
$masked.Password='123'
$addon=$toggle.Parent.Parent
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
SaveVisual $addon 'eye-plain.png' 3
$toggle.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
SaveVisual $addon 'eye-masked.png' 3
[void]$sectionMethod.Invoke($window,@('email')); Layout 1180 1500; Pump
$emailCard=Card ($window.Content.FindName('btnEmailTest'))
SaveVisual $emailCard.Parent 'email-cards.png'
[void]$sectionMethod.Invoke($window,@('about')); Layout 900 800; Pump
$status=$window.Content.FindName('lblUpdateStatus')
SaveVisual $status.Parent.Parent 'update-about.png'
$windowType.GetMethod('ExitApp',$flags).Invoke($window,@()) | Out-Null
