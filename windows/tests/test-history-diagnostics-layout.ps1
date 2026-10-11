# Synthetic fixtures only. No original config, history or log is read.
param([string]$ExePath = (Join-Path $PSScriptRoot '..\dist\codepass.exe'), [string]$FixtureDirectory = '', [string]$EvidenceDirectory = '')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
[Windows.Media.RenderOptions]::ProcessRenderMode=[Windows.Interop.RenderMode]::SoftwareOnly
Add-Type -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.Media.Visual].Assembly.Location,[Windows.DependencyObject].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location) -TypeDefinition @'
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
public static class LayoutCapture {
    public static bool Save(Visual visual, int width, int height, string path) {
        RenderTargetBitmap bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        byte[] pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        bool nonempty = pixels[((height / 2) * width + width / 2) * 4 + 3] > 0;
        if (!nonempty) return false;
        PngBitmapEncoder png = new PngBitmapEncoder();
        png.Frames.Add(BitmapFrame.Create(bitmap));
        using (FileStream output = File.Create(path)) png.Save(output);
        return nonempty;
    }
}
'@
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$configType=$asm.GetType('Codepass.Config'); $windowType=$asm.GetType('Codepass.MainWindow')
$programType=$asm.GetType('Codepass.Program'); $historyType=$asm.GetType('Codepass.History')
$itemType=$asm.GetType('Codepass.HistoryItem'); $diagnosticsType=$asm.GetType('Codepass.Diagnostics')
$root=if($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $PSScriptRoot ('..\build\history-diagnostics-fixtures-'+[Guid]::NewGuid().ToString('N')) }
$evidence=if($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else { Join-Path $root 'evidence' }
New-Item -ItemType Directory -Force -Path $root,$evidence | Out-Null
$pass=0; $fail=0
function Assert([string]$name,[bool]$ok) { if($ok) { $script:pass++; Write-Output ('PASS '+$name) } else { $script:fail++; Write-Output ('FAIL '+$name) } }
function Rejected([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
function Call($class,[string]$name,$target,[object[]]$values) {
    $args=New-Object object[] $values.Length
    for($i=0;$i -lt $values.Length;$i++) { if($null -ne $values[$i]) { $args[$i]=$values[$i].PSObject.BaseObject } }
    if($null -ne $target) { $target=$target.PSObject.BaseObject }
    $method=$class.GetMethods($flags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $values.Length } | Select-Object -First 1
    return ,($method.Invoke($target,$args))
}
function AddRecord([string]$channel,[string]$sender,[string]$code,[string]$raw) {
    $item=[Activator]::CreateInstance($itemType,$true)
    $item.Channel=$channel; $item.Source=$sender; $item.Code=$code; $item.Raw=$raw; $item.Time='10-11 12:00:00'
    [void](Call $historyType 'AddUnique' $null @($item))
}
function Pump {
    $frame=New-Object Windows.Threading.DispatcherFrame
    [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame.Continue=$false })
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Layout([int]$width,[int]$height) {
    $window.Width=$width; $window.Height=$height
    Pump
    $window.Content.Opacity=1
    $window.Content.Measure((New-Object Windows.Size($width,$height)))
    $window.Content.Arrange((New-Object Windows.Rect(0,0,$width,$height)))
    $window.Content.UpdateLayout()
    [void](Call $windowType 'UpdateResponsiveForms' $window @())
    $window.Content.UpdateLayout()
}
function Capture([string]$name,[int]$width,[int]$height) {
    Layout $width $height
    if ([LayoutCapture]::Save($window.Content,$width,$height,(Join-Path $evidence $name))) { Write-Output ('EVIDENCE_SAVED='+$name) }
    else { Write-Output ('EVIDENCE_UNAVAILABLE='+$name+' (desktop locked or render target unavailable; geometry checks still run)') }
}
function WithinParent($element) {
    $parent=[Windows.LogicalTreeHelper]::GetParent($element)
    if($parent -isnot [Windows.FrameworkElement]) { return $true }
    $point=$element.TranslatePoint((New-Object Windows.Point(0,0)),$parent)
    return ($point.X -ge -1 -and $point.X+$element.ActualWidth -le $parent.ActualWidth+1)
}
function WithinViewport($element,$viewport) {
    $point=$element.TranslatePoint((New-Object Windows.Point(0,0)),$viewport)
    return ($element.ActualWidth -gt 0 -and $point.X -ge -1 -and $point.X+$element.ActualWidth -le $viewport.ViewportWidth+1)
}
function CheckControls([string]$section,[string[]]$names) {
    $viewport=$content.FindName('settingsScroll')
    foreach($name in $names) { Assert ('layout:viewport-'+$section+'-'+$name) (WithinViewport ($content.FindName($name)) $viewport) }
}
$cfg=[Activator]::CreateInstance($configType,$true)
$cfg.PcIp='192.0.2.51'
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
$programType.GetField('appDir',$flags).SetValue($null,$root)
$configPath=Join-Path $root 'config.ini'; $cfg.Save($configPath)
[void](Call $historyType 'Init' $null @((Join-Path $root 'history.txt')))
AddRecord 'sms' 'ExampleShop' '314159' 'shop login code 314159'
AddRecord 'email' 'shop@example.invalid' '271828' 'ExampleShop security code 271828'
AddRecord 'email' 'OtherService' '161803' ''
$window=$windowType.GetConstructor($flags,$null,[Type[]]@($configType,[string]),$null).Invoke([object[]]@($cfg.PSObject.BaseObject,[string]$configPath))
$window.ShowActivated=$false
$window.Show(); Pump
$content=$window.Content; $search=$content.FindName('txtHistorySearch'); $list=$content.FindName('listHost')
Assert 'history:default-all-channels' ($list.Children.Count -eq 3)
$search.Text='exampleshop'
Assert 'history:case-insensitive-source-and-body' ($list.Children.Count -eq 2)
[void](Call $windowType 'SelectHistoryChannel' $window @('email'))
Assert 'history:search-and-channel-combined' ($list.Children.Count -eq 1)
$search.Text='shop 271828'
Assert 'history:multi-word-cross-field-match' ($list.Children.Count -eq 1)
$search.Text='missing-service'
Assert 'history:empty-search-feedback' ($list.Children.Count -eq 0 -and $content.FindName('lblHistoryEmpty').Visibility.ToString() -eq 'Visible')
AddRecord 'email' 'missing-service' '424242' ''
[void](Call $windowType 'RefreshHistory' $window @())
Assert 'history:new-record-keeps-filter' ($list.Children.Count -eq 1 -and $search.Text -eq 'missing-service')
$content.FindName('btnHistoryReset').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Assert 'history:reset-restores-all' ($list.Children.Count -eq 4 -and $search.Text.Length -eq 0)
[void](Call $windowType 'SelectHistoryChannel' $window @('sms'))
Assert 'history:sms-channel-only' ($list.Children.Count -eq 1)
[void](Call $windowType 'SelectHistoryChannel' $window @('all'))
Capture 'history-search.png' 1100 760
$emailInput=$content.FindName('txtEmailAccount'); $emailGrid=[Windows.LogicalTreeHelper]::GetParent($emailInput)
[void](Call $windowType 'SelectSection' $window @('email'))
Layout 780 760
Assert 'layout:email-labels-above-narrow' ([Windows.Controls.Grid]::GetColumn($emailInput) -eq 1 -and [Windows.Controls.Grid]::GetRow($emailInput) -eq 7 -and $emailGrid.ColumnDefinitions[0].Width.Value -eq 0)
Assert 'layout:email-status-before-connection' ($content.FindName('cardEmailStatus').TranslatePoint((New-Object Windows.Point(0,0)),$content).Y -lt $content.FindName('cardEmail').TranslatePoint((New-Object Windows.Point(0,0)),$content).Y)
Assert 'layout:connection-rule-advanced-visible' ($content.FindName('cardEmail').Visibility.ToString() -eq 'Visible' -and $content.FindName('cardEmailFilters').Visibility.ToString() -eq 'Visible' -and $content.FindName('cardQqPopup').Visibility.ToString() -eq 'Visible')
Assert 'layout:no-horizontal-email-scroll' ($content.FindName('settingsScroll').ExtentWidth -le $content.FindName('settingsScroll').ViewportWidth+1)
Assert 'layout:email-fields-within-parent' ((WithinParent $emailInput) -and (WithinParent ($content.FindName('cmbEmailProvider'))) -and (WithinParent ($content.FindName('txtEmailServer'))))
CheckControls 'email-narrow' @('cmbEmailProvider','txtEmailServer','txtEmailAccount','emailAuthHost','txtEmailKeywordAdd','btnEmailKeywordAdd','btnEmailKeywordDefault','txtEmailSenders','chkEmailJunk','chkEmailSaveBody','chkQqPopup','txtEmailPoll','btnEmailTest','btnEmailRecheck')
Capture 'email-narrow.png' 780 760
Layout 1280 850
Assert 'layout:email-wide-restores-label-column' ([Windows.Controls.Grid]::GetRow($emailInput) -eq 3 -and $emailGrid.ColumnDefinitions[0].Width.Value -eq 140)
Assert 'layout:email-wide-field-within-parent' (WithinParent $emailInput)
CheckControls 'email-wide' @('txtEmailAccount','emailAuthHost','txtEmailKeywordAdd','btnEmailKeywordDefault','chkQqPopup')
Capture 'email-wide.png' 1280 850
foreach($section in @('general','security')) {
    [void](Call $windowType 'SelectSection' $window @($section))
    Layout 780 760
    Assert ('layout:no-horizontal-scroll-'+$section) ($content.FindName('settingsScroll').ExtentWidth -le $content.FindName('settingsScroll').ViewportWidth+1)
    if($section -eq 'general') { CheckControls 'sms-narrow' @('txtPcIp','btnDetect','btnTestPc','tokenHost','ntfyTokenHost','txtNtfyServer','btnTestNtfy','txtFilterKeywords','txtFilterRegex','chkStartup','btnGen','btnImport','btnExport') }
    else {
        Assert 'layout:lock-action-hidden-until-enabled' ($content.FindName('btnLock').Visibility -eq [Windows.Visibility]::Collapsed)
        [void](Call $configType 'SetPassword' $cfg @('synthetic-layout-password'))
        [void](Call $windowType 'LoadValues' $window @())
        Layout 780 760
        CheckControls 'privacy-narrow' @('cmbPrivacy','cmbEmailPrivacy','chkAutoLock','btnSecurity','btnLock')
    }
    Capture ($section+'-narrow.png') 780 760
}
[void](Call $windowType 'SelectSection' $window @('general'))
Assert 'layout:sms-sections-visible' ($content.FindName('cardNtfy').Visibility.ToString() -eq 'Visible' -and $content.FindName('cardSmsFilters').Visibility.ToString() -eq 'Visible')
Assert 'layout:sms-controls-within-parent' ((WithinParent ($content.FindName('txtPcIp'))) -and (WithinParent ($content.FindName('btnDetect'))) -and (WithinParent ($content.FindName('btnTestPc'))))
[void](Call $windowType 'SelectSection' $window @('about'))
Layout 780 560
Assert 'layout:diagnostics-action-within-parent' (WithinParent ($content.FindName('btnDiagnostics')))
Capture 'about-narrow.png' 780 560
# Prove geometry assertion catches clipping even when horizontal scrolling is disabled.
$clipProbe=New-Object Windows.Controls.ScrollViewer
$clipProbe.HorizontalScrollBarVisibility=[Windows.Controls.ScrollBarVisibility]::Disabled
$clipInput=New-Object Windows.Controls.TextBox; $clipInput.Width=500; $clipProbe.Content=$clipInput
$clipProbe.Measure((New-Object Windows.Size(100,100))); $clipProbe.Arrange((New-Object Windows.Rect(0,0,100,100))); $clipProbe.UpdateLayout()
Assert 'layout:clipping-probe-rejected' (-not (WithinViewport $clipInput $clipProbe))
# Inject unmistakable synthetic values into every free-text surface.
$synthetic='DO-NOT-EXPORT-fixture'
foreach($field in @('Token','NtfyToken','NtfyTopic','NtfyServer','PcIp','EmailServer','EmailAccount','EmailAuth','EmailAuthEncRaw','EmailKeywords','EmailSenders','FilterKeywords','FilterRegex','EmailMailboxState','LockSalt','LockHash')) { $configType.GetField($field,$flags).SetValue($cfg,$synthetic+'-'+$field) }
AddRecord 'sms' ($synthetic+'-sender') '987654' ($synthetic+'-body')
$watcher=$asm.GetType('Codepass.EmailWatcher')
$watcher.GetField('Status',$flags).SetValue($null,'failure '+$synthetic)
$watcher.GetField('FolderStatus',$flags).SetValue($null,'folder '+$synthetic)
$watcher.GetField('LastMailResult',$flags).SetValue($null,$synthetic+'-result')
$watcher.GetField('RecentSummary',$flags).SetValue($null,$synthetic+'-summary')
foreach($log in @(('config save failed: '+$synthetic),('ntfy error: '+$synthetic),('arbitrary '+$synthetic),'email error category=network')) { [void](Call $diagnosticsType 'ObserveLog' $null @([string]$log)) }
$report=Call $diagnosticsType 'CreateReport' $null @($cfg)
Assert 'diagnostics:all-free-text-excluded' (-not $report.Contains($synthetic) -and -not $report.Contains('987654') -and -not $report.Contains('314159') -and -not $report.Contains('example.invalid') -and -not $report.Contains($root))
Assert 'diagnostics:known-event-category-retained' ($report.Contains('network'))
$checking=[string]([char]0x6B63)+[char]0x5728+[char]0x68C0+[char]0x67E5
$success=[string]([char]0x76EE)+[char]0x5F55+[char]0x68C0+[char]0x67E5+[char]0x6210+[char]0x529F
$noFolders=[string]([char]0x6CA1)+[char]0x6709+[char]0x989D+[char]0x5916+[char]0x63A5+[char]0x6536+[char]0x76EE+[char]0x5F55
$retry=[string]([char]0x91CD)+[char]0x8BD5
Assert 'diagnostics:folder-check-in-progress' ((Call $diagnosticsType 'StatusCategory' $null @($checking+' 2')) -ne (Call $diagnosticsType 'StatusCategory' $null @('unknown')))
Assert 'diagnostics:folder-success-classified' ((Call $diagnosticsType 'StatusCategory' $null @('2/2 '+$success)) -ne (Call $diagnosticsType 'StatusCategory' $null @('unknown')))
Assert 'diagnostics:no-extra-folders-classified' ((Call $diagnosticsType 'StatusCategory' $null @($noFolders)) -ne (Call $diagnosticsType 'StatusCategory' $null @('unknown')))
Assert 'diagnostics:folder-partial-failure-classified' ((Call $diagnosticsType 'StatusCategory' $null @('1/2 '+$success+' '+$retry)) -eq (Call $diagnosticsType 'StatusCategory' $null @($retry)))
$saved=Join-Path $root 'diagnostics.txt'
[void](Call $diagnosticsType 'SavePreview' $null @($saved,$report,$configPath))
Assert 'diagnostics:saved-report-equals-preview' ([IO.File]::ReadAllText($saved) -eq $report)
$protectedFixture=Join-Path $root 'hardlink-original.txt'; $linkedReport=Join-Path $root 'hardlink-report.txt'
[IO.File]::WriteAllText($protectedFixture,'owned synthetic original')
if((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'Refusing linked fixture directory' }
# Both entries are owned fixtures; audit their type before exercising atomic replacement.
New-Item -ItemType HardLink -Path $linkedReport -Value $protectedFixture | Out-Null
Assert 'diagnostics:hardlink-fixture-audited' ((Get-Item -LiteralPath $linkedReport).LinkType -eq 'HardLink')
[void](Call $diagnosticsType 'SavePreview' $null @($linkedReport,$report,$configPath))
Assert 'diagnostics:hardlink-original-preserved' ([IO.File]::ReadAllText($protectedFixture) -eq 'owned synthetic original' -and [IO.File]::ReadAllText($linkedReport) -eq $report)
foreach($protected in @($configPath,(Join-Path $root 'history.txt'),(Join-Path $root 'app.log'),$ExePath)) { Assert ('diagnostics:refuse-runtime-target-'+[IO.Path]::GetFileName($protected)) (Rejected { Call $diagnosticsType 'SavePreview' $null @($protected,$report,$configPath) }) }
$dialog=Call $windowType 'CreateDiagnosticsWindow' $window @()
Assert 'diagnostics:readonly-preview-visible' ($dialog.Content.Children[1].IsReadOnly -and $dialog.Content.Children[1].Text.Contains('network'))
$dialog.ShowActivated=$false; $dialog.Show(); Pump
$cfg.LockEnabled=$true
[void](Call $windowType 'LockWindow' $window @())
Assert 'diagnostics:lock-closes-preview' ($null -eq $windowType.GetField('diagnosticsWindow',$flags).GetValue($window))
$cfg.AutoLockEnabled=$true
$content.FindName('lockOverlay').Visibility=[Windows.Visibility]::Collapsed
$dialog=Call $windowType 'CreateDiagnosticsWindow' $window @()
$dialog.ShowActivated=$false; $dialog.Show(); Pump
$dialog.WindowState=[Windows.WindowState]::Minimized; Pump
Assert 'diagnostics:minimize-preview-auto-locks' ($content.FindName('lockOverlay').Visibility -eq [Windows.Visibility]::Visible -and $null -eq $windowType.GetField('diagnosticsWindow',$flags).GetValue($window))
# Restore the persisted synthetic config before exit to avoid saving injected invalid fields.
$restored=Call $configType 'Load' $null @($configPath)
[void](Call $configType 'CopyFrom' $cfg @($restored))
[void](Call $windowType 'LoadValues' $window @())
[void](Call $windowType 'ExitApp' $window @())
Write-Output ('FIXTURES='+$root)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0) { exit 1 }
