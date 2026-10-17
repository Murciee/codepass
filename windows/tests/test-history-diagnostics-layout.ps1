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
        if (!nonempty) {
            FrameworkElement content = visual as FrameworkElement;
            Window owner = content == null ? null : Window.GetWindow(content);
            if (owner == null || !object.ReferenceEquals(owner.Content, content)) return false;
            try {
                owner.Content = null;
                content.Measure(new Size(width, height)); content.Arrange(new Rect(0, 0, width, height)); content.UpdateLayout();
                bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(content); bitmap.CopyPixels(pixels, width * 4, 0);
                nonempty = pixels[((height / 2) * width + width / 2) * 4 + 3] > 0;
            } finally { owner.Content = content; content.UpdateLayout(); }
            if (!nonempty) return false;
        }
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
    [void](Call $programType 'RestoreLatestFromHistory' $null @())
}
function Pump {
    $frame=New-Object Windows.Threading.DispatcherFrame
    [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle,[Action]{ $frame.Continue=$false })
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function EnterText($box,[string]$text) {
    $composition=New-Object Windows.Input.TextComposition([Windows.Input.InputManager]::Current,$box,$text)
    $event=New-Object Windows.Input.TextCompositionEventArgs([Windows.Input.Keyboard]::PrimaryDevice,$composition)
    $event.RoutedEvent=[Windows.Input.TextCompositionManager]::TextInputEvent
    $box.RaiseEvent($event)
    Pump
}
function CheckRevealInput($revealer,[string]$name) {
    $type=$revealer.GetType()
    $maskedInput=$type.GetField('masked',$flags).GetValue($revealer)
    $plainInput=$type.GetField('plain',$flags).GetValue($revealer)
    $eye=$type.GetField('toggle',$flags).GetValue($revealer)
    if($plainInput.Visibility -eq [Windows.Visibility]::Visible) {
        $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
    }
    $expected='123'
    $maskedInput.Password=$expected
    for($round=0;$round -lt 3;$round++) {
        [void]$eye.Focus()
        $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
        Assert ($name+':plain-caret-'+$round) ($plainInput.CaretIndex -eq $expected.Length -and $plainInput.SelectionLength -eq 0)
        EnterText $plainInput '4'
        $expected+='4'
        Assert ($name+':plain-append-'+$round) ($plainInput.Text -ceq $expected)
        [void]$eye.Focus()
        $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
        EnterText $maskedInput '5'
        $expected+='5'
        Assert ($name+':masked-append-'+$round) ($maskedInput.Password -ceq $expected)
    }
    $expected=('1234567890'*24)
    $maskedInput.Password=$expected
    $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
    EnterText $plainInput '7'; $expected+='7'
    Assert ($name+':long-plain-append') ($plainInput.Text -ceq $expected)
    $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
    EnterText $maskedInput '8'; $expected+='8'
    Assert ($name+':long-masked-append') ($maskedInput.Password -ceq $expected)
    $maskedInput.Password=''
    $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    $eye.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))); Pump
    EnterText $maskedInput '6'
    Assert ($name+':empty-rapid-toggle-append') ($maskedInput.Password -ceq '6')
    $maskedInput.Password=''
}
function FlushSearch {
    [void](Call $windowType 'RefreshHistory' $window @())
    Pump
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
function InSubtree($element,$ancestor) {
    $node=$element
    while($node -ne $null) { if([object]::ReferenceEquals($node,$ancestor)) { return $true }; $node=[Windows.LogicalTreeHelper]::GetParent($node) }
    return $false
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
Assert 'history:removed-status-and-setup-controls' ($null -eq $content.FindName('receiveStatusPanel') -and $null -eq $content.FindName('firstRunActions') -and $null -eq $content.FindName('btnSetupSms') -and $null -eq $content.FindName('btnSetupEmail'))
Assert 'history:action-header-removed-buttons-kept' ($content.FindName('historyHeaders').Children.Count -eq 4 -and $list.Children[0].Child.Children[4] -is [Windows.Controls.Button])
Assert 'history:retention-always-visible' ($content.FindName('historySettings') -is [Windows.Controls.StackPanel] -and $content.FindName('txtHistoryRetention').IsVisible -and $content.FindName('historyRetentionHint').IsVisible)
$accent=$windowType.GetField('AccentTx',$flags).GetValue($null)
Assert 'history:only-newest-code-blue' ($list.Children[0].Child.Children[2].Foreground -eq $accent -and $list.Children[1].Child.Children[2].Foreground -eq $list.Children[1].Child.Children[0].Foreground -and $list.Children[2].Child.Children[2].Foreground -eq $list.Children[2].Child.Children[0].Foreground)
$colorItems=Call $historyType 'Snapshot' $null @();$oldCode=$colorItems[1].Code
$colorItems[1].Code=$colorItems[0].Code
FlushSearch
Assert 'history:identical-code-does-not-highlight-old-record' ($list.Children[0].Child.Children[2].Foreground -eq $accent -and $list.Children[1].Child.Children[2].Foreground -ne $accent)
$colorItems[1].Code=$oldCode;FlushSearch
$search.Text='exampleshop'
FlushSearch
Assert 'history:case-insensitive-source-and-body' ($list.Children.Count -eq 2)
Assert 'history:filter-does-not-promote-old-code' ($list.Children[0].Child.Children[2].Foreground -ne $accent -and $list.Children[1].Child.Children[2].Foreground -ne $accent)
[void](Call $windowType 'SelectHistoryChannel' $window @('email'))
Assert 'history:search-and-channel-combined' ($list.Children.Count -eq 1)
$search.Text='shop 271828'
FlushSearch
Assert 'history:multi-word-cross-field-match' ($list.Children.Count -eq 1)
$search.Text='missing-service'
FlushSearch
Assert 'history:empty-search-feedback' ($list.Children.Count -eq 0 -and $content.FindName('lblHistoryEmpty').Visibility.ToString() -eq 'Visible')
AddRecord 'email' 'missing-service' '424242' ''
[void](Call $windowType 'RefreshHistory' $window @())
Assert 'history:new-record-keeps-filter' ($list.Children.Count -eq 1 -and $search.Text -eq 'missing-service')
$content.FindName('btnHistoryReset').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Assert 'history:reset-restores-all' ($list.Children.Count -eq 4 -and $search.Text.Length -eq 0)
[void](Call $windowType 'SelectHistoryChannel' $window @('sms'))
Assert 'history:sms-channel-only' ($list.Children.Count -eq 1)
[void](Call $windowType 'SelectHistoryChannel' $window @('all'))
Layout 1100 760
$search.Text='shop 271828';FlushSearch
$row=$list.Children[0];$actions=$row.Child.Children[4]
$actions.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Pump
$menu=$windowType.GetField('historyActionsMenu',$flags).GetValue($window)
Assert 'history:actions-menu-opens' ($null -ne $menu -and $menu.IsOpen -and $menu.Items.Count -eq 2 -and [object]::ReferenceEquals($menu.PlacementTarget,$actions))
$delete=$menu.Items[1]
$delete.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.MenuItem]::ClickEvent)))
Pump
Assert 'history:delete-keeps-search-filter' ($search.Text -eq 'shop 271828' -and $list.Children.Count -eq 0 -and (Call $historyType 'Snapshot' $null @()).Count -eq 3)
[void](Call $historyType 'Init' $null @((Join-Path $root 'history.txt')))
Assert 'history:menu-delete-persists' ((Call $historyType 'Snapshot' $null @()).Count -eq 3 -and @((Call $historyType 'Snapshot' $null @())|Where-Object{$_.Code -eq '271828'}).Count -eq 0)
$delete.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.MenuItem]::ClickEvent)))
Assert 'history:stale-menu-cannot-delete-other-record' ((Call $historyType 'Snapshot' $null @()).Count -eq 3)
$content.FindName('btnHistoryReset').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
$actions=$list.Children[0].Child.Children[4]
$actions.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
$menu=$windowType.GetField('historyActionsMenu',$flags).GetValue($window);$delete=$menu.Items[1]
$cfg.LockEnabled=$true
[void](Call $windowType 'LockWindow' $window @())
Assert 'history:lock-closes-actions-menu' (-not $menu.IsOpen -and $null -eq $windowType.GetField('historyActionsMenu',$flags).GetValue($window))
$delete.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.MenuItem]::ClickEvent)))
Assert 'history:locked-delete-is-blocked' ((Call $historyType 'Snapshot' $null @()).Count -eq 3)
$cfg.LockEnabled=$false;$content.FindName('lockOverlay').Visibility=[Windows.Visibility]::Collapsed
[void](Call $windowType 'SetLockedContent' $window @($false))
foreach($size in @(640,1100)) {
    Layout $size 760
    Assert ('history:actions-fit-row-'+$size) (WithinParent $actions)
}
Capture 'history-search.png' 1100 760
$emailInput=$content.FindName('txtEmailAccount')
[void](Call $windowType 'SelectSection' $window @('email'))
Capture 'email-default.png' 780 760
Layout 780 760
Assert 'layout:email-status-card-removed' ($null -eq $content.FindName('cardEmailStatus') -and $null -eq $content.FindName('btnEmailRecheck'))
Assert 'layout:email-advanced-always-visible' ($content.FindName('emailAdvanced') -is [Windows.Controls.StackPanel] -and $content.FindName('emailAdvanced').IsVisible)
Assert 'layout:email-poll-compact' ($content.FindName('txtEmailPoll').ActualWidth -le 120)
Assert 'copy:email-qq-long-description-removed' ($null -eq $content.FindName('lblQqPopupRisk'))
Assert 'layout:connection-rule-advanced-visible' ($content.FindName('cardEmail').Visibility.ToString() -eq 'Visible' -and $content.FindName('cardEmailFilters').Visibility.ToString() -eq 'Visible' -and $content.FindName('cardQqPopup').Visibility.ToString() -eq 'Visible')
Assert 'layout:no-horizontal-email-scroll' ($content.FindName('settingsScroll').ExtentWidth -le $content.FindName('settingsScroll').ViewportWidth+1)
Assert 'layout:email-fields-within-parent' ((WithinParent $emailInput) -and (WithinParent ($content.FindName('cmbEmailProvider'))) -and (WithinParent ($content.FindName('txtEmailServer'))))
CheckControls 'email-narrow' @('cmbEmailProvider','txtEmailServer','txtEmailAccount','emailAuthHost','btnEmailKeywordAdd','btnEmailKeywordDefault','txtEmailSenders','chkEmailJunk','chkEmailSaveBody','chkQqPopup','txtEmailPoll','btnEmailTest','btnEmailSave')
Capture 'email-narrow.png' 780 760
Layout 1280 850
Assert 'layout:email-wide-field-within-parent' (WithinParent $emailInput)
CheckControls 'email-wide' @('txtEmailAccount','emailAuthHost','btnEmailKeywordDefault','chkQqPopup','btnEmailTest','btnEmailSave')
Assert 'copy:email-qq-hint-stays-concise' (([string]$content.FindName('chkQqPopup').Content).Length -lt 20 -and ([string]$content.FindName('chkQqPopup').ToolTip).Length -lt 40)
Capture 'email-wide.png' 1280 850
$content.FindName('settingsScroll').ScrollToBottom();Pump
Capture 'email-advanced-wide.png' 1280 850
Layout 780 760
$content.FindName('settingsScroll').ScrollToBottom();Pump
Capture 'email-advanced-narrow.png' 780 760
foreach($section in @('general','security')) {
    [void](Call $windowType 'SelectSection' $window @($section))
    Layout 780 760
    Assert ('layout:no-horizontal-scroll-'+$section) ($content.FindName('settingsScroll').ExtentWidth -le $content.FindName('settingsScroll').ViewportWidth+1)
    if($section -eq 'general') { CheckControls 'sms-narrow' @('txtPcIp','btnDetect','btnTestPc','lblPcStatus','tokenHost','ntfyTokenHost','txtNtfyServer','btnTestNtfy','txtFilterKeywords','txtFilterRegex','btnAcceptKeywordAdd','btnAcceptKeywordDefault','chkStartup','btnGen') }
    else {
        Assert 'layout:lock-action-hidden-until-enabled' ($content.FindName('btnLock').Visibility -eq [Windows.Visibility]::Collapsed)
        # The lock screen uses the same inline eye: one click reveals, another restores the mask.
        Assert 'unlock:eye-starts-masked' ($content.FindName('txtUnlockPlain').Visibility -eq [Windows.Visibility]::Collapsed)
        $content.FindName('btnUnlockEye').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        Assert 'unlock:eye-reveals-plain' ($content.FindName('txtUnlockPlain').Visibility -eq [Windows.Visibility]::Visible -and $content.FindName('txtUnlockPassword').Visibility -eq [Windows.Visibility]::Collapsed)
        $content.FindName('btnUnlockEye').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        Assert 'unlock:eye-restores-mask' ($content.FindName('txtUnlockPlain').Visibility -eq [Windows.Visibility]::Collapsed -and $content.FindName('txtUnlockPassword').Visibility -eq [Windows.Visibility]::Visible)
        $unlockAddon=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($content.FindName('txtUnlockPassword'))))
        Assert 'unlock:field-uses-input-addon' ($unlockAddon -is [Windows.Controls.Border] -and [Math]::Abs([double]$unlockAddon.Width-300) -le 1)
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
# Settings page model: one item per SettingsCard. Every card shares one box, every title
# starts on the same left line as the group titles, every control hugs the same right line.
function RightEdge($element,$viewport) { return $element.TranslatePoint((New-Object Windows.Point($element.ActualWidth,0)),$viewport).X }
function LeftEdge($element,$viewport) { return $element.TranslatePoint((New-Object Windows.Point(0,0)),$viewport).X }
function SettingsCards($node) {
    $grid=$node -as [Windows.Controls.Grid]
    if($null -ne $grid -and ($grid.Tag -eq 'SettingsCard' -or $grid.Tag -eq 'SettingsCardStacked')) { ,$grid }
    foreach($child in @([Windows.LogicalTreeHelper]::GetChildren($node))) { if($child -is [Windows.FrameworkElement]) { SettingsCards $child } }
}
function CheckCards([string]$section,[int]$size,[bool]$compact) {
    $cards=@(SettingsCards $content | Where-Object { $_ -is [Windows.Controls.Grid] -and $_.IsVisible })
    Assert ('cards:found-'+$section+'-'+$size) ($cards.Count -ge 4)
    $box=[Windows.LogicalTreeHelper]::GetParent($cards[0])
    $boxLeft=(LeftEdge $box $content); $boxRight=(RightEdge $box $content)
    # Group titles and descriptions share the same left line as the card text (padding 20).
    foreach($name in @('cardGeneral','cardNetwork','cardNtfy','cardSmsFilters','cardMore','cardEmail','cardEmailFilters','cardQqPopup','cardNotify','cardSecurity')) {
        $group=$content.FindName($name)
        if($group -eq $null -or -not $group.IsVisible) { continue }
        Assert ('cards:group-title-left-'+$section+'-'+$size+'-'+$name) ([Math]::Abs((LeftEdge $group.Child.Children[0] $content)-($boxLeft+20)) -le 2)
    }
    $index=0
    foreach($card in $cards) {
        $index++
        $cardBox=[Windows.LogicalTreeHelper]::GetParent($card)
        Assert ('cards:same-box-'+$section+'-'+$size+'-'+$index) ([Math]::Abs((LeftEdge $cardBox $content)-$boxLeft) -le 1 -and [Math]::Abs((RightEdge $cardBox $content)-$boxRight) -le 1)
        $header=@([Windows.LogicalTreeHelper]::GetChildren($card) | Where-Object { $_ -is [Windows.FrameworkElement] })[0]
        $title=$header
        if($title -isnot [Windows.Controls.TextBlock]) {
            $inner=@([Windows.LogicalTreeHelper]::GetChildren($title) | Where-Object { $_ -is [Windows.Controls.TextBlock] })
            if($inner.Count -gt 0) { $title=$inner[0] }
        }
        Assert ('cards:title-left-'+$section+'-'+$size+'-'+$index) ([Math]::Abs((LeftEdge $title $content)-($boxLeft+20)) -le 2)
        $contentElement=$card.Children[1]
        # Every control's right edge sits on the same line (full-width blocks stretch to it too).
        Assert ('cards:control-right-'+$section+'-'+$size+'-'+$index) ([Math]::Abs((RightEdge $contentElement $content)-($boxRight-20)) -le 2)
        # The two columns share the same vertical center (a blank description must not push the text up).
        if($card.Tag -eq 'SettingsCard' -and -not $compact) {
            $left=$null; $right=$null
            foreach($child in [Windows.LogicalTreeHelper]::GetChildren($card)) {
                if($child -isnot [Windows.FrameworkElement]) { continue }
                if($left -eq $null) { $left=$child } else { if($right -eq $null) { $right=$child } }
            }
            if($left -ne $null -and $right -ne $null) {
                $leftCenter=$left.TranslatePoint((New-Object Windows.Point(0,([double]$left.ActualHeight/2))),$card).Y
                $rightCenter=$right.TranslatePoint((New-Object Windows.Point(0,([double]$right.ActualHeight/2))),$card).Y
                $cardCenter=[double]$card.ActualHeight/2
                # Both columns must sit on the card's own middle (extra min-height space lands at the bottom).
                Assert ('cards:row-centered-'+$section+'-'+$size+'-'+$index) ([Math]::Abs($leftCenter-$cardCenter) -le 2 -and [Math]::Abs($rightCenter-$cardCenter) -le 2)
            }
        }
        $boxWidth=$boxRight-$boxLeft
        if($card.Tag -eq 'SettingsCardStacked' -and [Math]::Abs($contentElement.ActualWidth-($boxWidth-42)) -le 4) {
            Assert ('cards:content-full-width-'+$section+'-'+$size+'-'+$index) ([Math]::Abs((LeftEdge $contentElement $content)-($boxLeft+21)) -le 2)
        }
    }
}
# Toggle cards: the whole row is clickable and flips its switch.
function ToggleCards($node,$list) {
    $border=$node -as [Windows.Controls.Border]
    if($null -ne $border -and $border.Tag -eq 'SettingsToggle') { [void]$list.Add($border) }
    foreach($child in @([Windows.LogicalTreeHelper]::GetChildren($node))) { if($child -is [Windows.FrameworkElement]) { ToggleCards $child $list } }
}
foreach($size in @(640,780,1100,1280)) {
    $compactShell=$size -lt 700
    [void](Call $windowType 'SelectSection' $window @('general'))
    Layout $size 850
    $inputWidth=[double]$content.Resources['SettingsInputWidth']
    # Content-sized fields: the box fits what the field holds (5 digits, IPv4, 2 digits...), not a fixed 180.
    $contentWidths=@{txtPort=72;txtPcIp=132;txtMin=46;txtMax=46}
    foreach($name in $contentWidths.Keys) {
        Assert ('cards:content-width-'+$size+'-'+$name) ([Math]::Abs($content.FindName($name).ActualWidth-$contentWidths[$name]) -le 1)
    }
    $addonWidths=@{txtClear=92;txtSmsMaxLength=118}
    foreach($name in $addonWidths.Keys) {
        $addon=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($content.FindName($name)))
        Assert ('cards:content-addon-width-'+$size+'-'+$name) ([Math]::Abs($addon.ActualWidth-$addonWidths[$name]) -le 1)
    }
    foreach($name in @('txtNtfyServer','txtNtfyTopic')) {
        Assert ('cards:input-width-'+$size+'-'+$name) ([Math]::Abs($content.FindName($name).ActualWidth-$inputWidth) -le 1)
    }
    # Secret field: same width and same outer edge as the plain inputs (the eye is inside the box).
    foreach($hostName in @('tokenHost','ntfyTokenHost')) {
        $secretHost=$content.FindName($hostName)
        $addon=@([Windows.LogicalTreeHelper]::GetChildren($secretHost) | Where-Object { $_ -is [Windows.Controls.Border] })[0]
        Assert ('cards:secret-addon-width-'+$size+'-'+$hostName) ([Math]::Abs($addon.ActualWidth-$inputWidth) -le 1)
        Assert ('cards:secret-addon-right-'+$size+'-'+$hostName) ([Math]::Abs((RightEdge $addon $content)-(RightEdge $secretHost $content)) -le 1)
    }
    # Connectivity actions live in the description line, never beside their input.
    foreach($pair in @(@('btnTestNtfy','txtNtfyServer'),@('btnDetect','txtPcIp'),@('btnTestPc','txtPcIp'))) {
        $action=$content.FindName($pair[0]); $inputBox=$content.FindName($pair[1])
        Assert ('cards:link-in-description-'+$size+'-'+$pair[0]) (-not [object]::ReferenceEquals([Windows.LogicalTreeHelper]::GetParent($action),[Windows.LogicalTreeHelper]::GetParent($inputBox)))
    }
    CheckCards 'sms' $size $compactShell
    CheckControls ('aligned-sms-'+$size) @('txtNtfyServer','ntfyTokenHost','btnTestPc','btnDetect')
    # Toggle motion is code-driven, so it survives the animation stripping Windows reports over RDP.
    $toggle=$content.FindName('chkStartup')
    $wasOn=($toggle.IsChecked -eq $true)
    $toggle.IsChecked=-not $wasOn
    $toggle.ApplyTemplate()
    $shift=$toggle.Template.FindName('KnobShift',$toggle)
    Assert ('cards:toggle-motion-'+$size) ($null -ne $shift -and $shift.HasAnimatedProperties)
    $final=0; if($toggle.IsChecked -eq $true) { $final=20 }
    # The animation is driven by the render clock; call the static sync (same path as loading settings) to verify the end position.
    $sync=$windowType.GetMethods($flags) | Where-Object { $_.Name -eq 'AnimateToggle' -and $_.GetParameters().Count -eq 2 } | Select-Object -First 1
    [void]$sync.Invoke($window,@($toggle,$true))
    Assert ('cards:toggle-knob-lands-'+$size) ([Math]::Abs($shift.X-$final) -lt 0.6)
    $toggle.IsChecked=$wasOn
    # Keyword add box stays hidden until Add is pressed.
    $acceptInput=$content.FindName('txtAcceptKeywordAdd')
    Assert ('cards:accept-keyword-input-hidden-'+$size) ($acceptInput.Visibility -eq [Windows.Visibility]::Collapsed)
    $content.FindName('btnAcceptKeywordAdd').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Assert ('cards:accept-keyword-input-shown-on-add-'+$size) ($acceptInput.Visibility -eq [Windows.Visibility]::Visible)
    $acceptInput.Visibility=[Windows.Visibility]::Collapsed
    if($size -eq 1100) {
        # Both directions must retain append-at-end behavior after focus and layout settle.
        $secret=$windowType.GetField('fldNtfyToken',$flags).GetValue($window)
        $secretType=$secret.GetType()
        $maskedBox=$secretType.GetField('masked',$flags).GetValue($secret)
        $plainBox=$secretType.GetField('plain',$flags).GetValue($secret)
        $revealBtn=$secretType.GetField('toggle',$flags).GetValue($secret)
        # Icon has two states: open eye and closed eye (both vector, lashes included).
        $closedIcon=$null; $openIcon=$null
        foreach($iconChild in $revealBtn.Content.Children) {
            if($iconChild.Tag -eq 'RevealOpen') { $openIcon=$iconChild }
            if($iconChild.Tag -eq 'RevealClosed') { $closedIcon=$iconChild }
        }
        Assert ('cards:reveal-icon-two-states-'+$size) ($null -ne $closedIcon -and $null -ne $openIcon -and $closedIcon.Visibility.ToString() -eq 'Collapsed' -and $openIcon.Visibility.ToString() -eq 'Visible')
        $maskedBox.Password='caret-probe'
        $revealBtn.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        Assert ('cards:reveal-shows-plain-'+$size) ($plainBox.Visibility.ToString() -eq 'Visible' -and $maskedBox.Visibility.ToString() -eq 'Collapsed' -and $closedIcon.Visibility.ToString() -eq 'Visible' -and $openIcon.Visibility.ToString() -eq 'Collapsed')
        Pump
        Assert ('cards:reveal-caret-at-end-'+$size) ($plainBox.Text -eq 'caret-probe' -and $plainBox.CaretIndex -eq 'caret-probe'.Length)
        $revealBtn.RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
        Assert ('cards:reveal-restores-mask-'+$size) ($maskedBox.Visibility.ToString() -eq 'Visible' -and $plainBox.Visibility.ToString() -eq 'Collapsed' -and $closedIcon.Visibility.ToString() -eq 'Collapsed' -and $openIcon.Visibility.ToString() -eq 'Visible')
        CheckRevealInput $secret 'reveal:settings'
        foreach($state in @($openIcon,$closedIcon)) {
            $state.Visibility=[Windows.Visibility]::Visible
            $window.Content.UpdateLayout()
            $canvas=$state.Children[0]
            $bounds=[Windows.Media.VisualTreeHelper]::GetDescendantBounds($canvas)
            $inButton=$canvas.TransformToAncestor($revealBtn).TransformBounds($bounds)
            Assert ('reveal:ink-centered-'+$state.Tag) ([Math]::Abs($inButton.X+$inButton.Width/2-$revealBtn.ActualWidth/2) -le 0.6 -and [Math]::Abs($inButton.Y+$inButton.Height/2-$revealBtn.ActualHeight/2) -le 0.6)
            $state.Visibility=[Windows.Visibility]::Collapsed
        }
        $openIcon.Visibility=[Windows.Visibility]::Visible
        Capture 'sms-aligned-wide.png' 1100 850
        $content.FindName('settingsScroll').ScrollToBottom(); Pump
        Capture 'sms-filters-wide.png' 1100 850
    }
    [void](Call $windowType 'SelectSection' $window @('email'))
    Layout $size 850
    $account=$content.FindName('txtEmailAccount')
    Assert ('cards:email-account-width-'+$size) ([Math]::Abs($account.ActualWidth-$inputWidth) -le 1)
    $emailPollAddon=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($content.FindName('txtEmailPoll')))
    Assert ('cards:content-addon-width-'+$size+'-txtEmailPoll') ([Math]::Abs($emailPollAddon.ActualWidth-92) -le 1)
    Assert ('cards:content-width-'+$size+'-cmbEmailProvider') ([Math]::Abs($content.FindName('cmbEmailProvider').ActualWidth-130) -le 1)
    # Keyword add box stays hidden until Add is pressed.
    $keywordInput=$content.FindName('txtEmailKeywordAdd')
    Assert ('cards:keyword-input-hidden-'+$size) ($keywordInput.Visibility -eq [Windows.Visibility]::Collapsed)
    $content.FindName('btnEmailKeywordAdd').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
    Assert ('cards:keyword-input-shown-on-add-'+$size) ($keywordInput.Visibility -eq [Windows.Visibility]::Visible)
    $keywordInput.Visibility=[Windows.Visibility]::Collapsed
    $emailPage=$windowType.GetField('emailPage',$flags).GetValue($window)
    $secret=$emailPage.GetType().GetField('fldEmailAuth',$flags).GetValue($emailPage)
    if($size -eq 1100) { CheckRevealInput $secret 'reveal:email' }
    $masked=$secret.GetType().GetField('masked',$flags).GetValue($secret)
    $addon=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($masked)))
    Assert ('cards:email-secret-addon-width-'+$size) ([Math]::Abs($addon.ActualWidth-$inputWidth) -le 1)
    CheckCards 'email' $size $compactShell
    $testButton=$content.FindName('btnEmailTest');$saveButton=$content.FindName('btnEmailSave')
    $actions=[Windows.LogicalTreeHelper]::GetParent($testButton)
    $testPoint=$testButton.TranslatePoint((New-Object Windows.Point(0,0)),$content)
    $savePoint=$saveButton.TranslatePoint((New-Object Windows.Point(0,0)),$content)
    Assert ('cards:email-actions-same-row-with-gap-'+$size) ([Math]::Abs($testPoint.Y-$savePoint.Y) -le 1 -and $savePoint.X-$testPoint.X-$testButton.ActualWidth -ge 7)
    if($size -eq 1100) { Capture 'email-cards-wide.png' 1100 850 }
    [void](Call $windowType 'SelectSection' $window @('security'))
    Layout $size 850
    CheckCards 'privacy' $size $compactShell
}
# The same interaction is used by the lock overlay and both password-dialog fields.
$revealerType=$asm.GetType('Codepass.PasswordRevealer')
$overlay=$content.FindName('lockOverlay')
$overlay.Visibility=[Windows.Visibility]::Visible; Pump
$lockProbe=$windowType.GetField('unlockRevealer',$flags).GetValue($window)
CheckRevealInput $lockProbe 'reveal:lock-overlay'
$overlay.Visibility=[Windows.Visibility]::Collapsed
$dialogType=$asm.GetType('Codepass.PasswordSettingsDialog')
$dialog=$dialogType.GetConstructors($flags)[0].Invoke([object[]]@($window.PSObject.BaseObject,$cfg.PSObject.BaseObject,[string]$configPath,$false))
$dialog.ShowActivated=$false; $dialog.Show(); Pump
$dialogRevealers=$dialogType.GetField('revealers',$flags).GetValue($dialog)
for($i=0;$i -lt $dialogRevealers.Count;$i++) {
    $dialogMasked=$revealerType.GetField('masked',$flags).GetValue($dialogRevealers[$i])
    if($dialogMasked.IsVisible) { CheckRevealInput $dialogRevealers[$i] ('reveal:dialog-'+$i) }
}
$dialog.Close()
# Import/export moved out of the settings page and into About.
[void](Call $windowType 'SelectSection' $window @('about'))
Layout 900 800
$aboutPage=$content.FindName('pageAbout')
Assert 'layout:import-export-moved-to-about' ((InSubtree ($content.FindName('btnImport')) $aboutPage) -and (InSubtree ($content.FindName('btnExport')) $aboutPage))
Assert 'layout:import-export-left-settings' (-not (InSubtree ($content.FindName('btnImport')) $content.FindName('pageSet')))
Capture 'about-wide.png' 900 800
[void](Call $windowType 'SelectSection' $window @('general'))
# Test message: simulate a received SMS and email through the real delivery path.
$dryWas=$cfg.Dry; $cfg.Dry=$false
$historyBefore=(Call $historyType 'Snapshot' $null @()).Count
$content.FindName('btnTestSms').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Pump
$historyAfterSms=(Call $historyType 'Snapshot' $null @()).Count
Assert 'settings:test-sms-adds-record' ($historyAfterSms -eq $historyBefore+1)
$content.FindName('btnTestEmail').RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent)))
Pump
$historyAfterEmail=(Call $historyType 'Snapshot' $null @()).Count
Assert 'settings:test-email-adds-record' ($historyAfterEmail -eq $historyAfterSms+1)
$cfg.Dry=$dryWas
$toggleList=New-Object 'System.Collections.Generic.List[object]'
ToggleCards $content $toggleList
Assert 'cards:toggles-present' ($toggleList.Count -ge 8)
[void](Call $windowType 'SelectSection' $window @('general'))
Layout 1100 850
$tipBox=$content.FindName('chkTip')
$tipCard=[Windows.LogicalTreeHelper]::GetParent([Windows.LogicalTreeHelper]::GetParent($tipBox))
$tipBefore=$tipBox.IsChecked
$clickArgs=New-Object Windows.Input.MouseButtonEventArgs([Windows.Input.Mouse]::PrimaryDevice,0,[Windows.Input.MouseButton]::Left)
$clickArgs.RoutedEvent=[Windows.UIElement]::MouseLeftButtonUpEvent
$tipCard.RaiseEvent($clickArgs); Pump
Assert 'cards:toggle-row-click-flips' ($tipBox.IsChecked -ne $tipBefore)
$tipCard.RaiseEvent($clickArgs); Pump
Assert 'cards:toggle-row-click-restores' ($tipBox.IsChecked -eq $tipBefore)
Assert 'cards:toggle-card-cursor' ($tipCard.Cursor.ToString() -eq 'Hand')
# Form rows: a label cell and its input cell must share one vertical centre.
# Cell geometry belongs to InitializeResponsiveForms alone, so a label must never
# carry its own Height or Padding. A stray one silently pushes that card's labels
# below their inputs, which is how every newly added card drifted out of line.
function LabelTextCentre($label,$viewport) {
    $top=$label.TranslatePoint((New-Object Windows.Point(0,0)),$viewport).Y
    return $top+$label.Padding.Top+($label.FontFamily.LineSpacing*$label.FontSize)/2
}
function EntryCentre($entry,$viewport) {
    $top=$entry.TranslatePoint((New-Object Windows.Point(0,0)),$viewport).Y
    return $top+$entry.ActualHeight/2
}
function FirstEntry($element) {
    if($element -is [Windows.Controls.TextBox] -or $element -is [Windows.Controls.PasswordBox] -or $element -is [Windows.Controls.ComboBox]) { return $element }
    foreach($child in @([Windows.LogicalTreeHelper]::GetChildren($element))) {
        if($child -is [Windows.FrameworkElement]) { $found=FirstEntry $child; if($null -ne $found) { return $found } }
    }
    return $null
}
function FormLabelRows($node) {
    $grid=$node -as [Windows.Controls.Grid]
    if($null -ne $grid -and $grid.ColumnDefinitions.Count -eq 2 -and $grid.ColumnDefinitions[0].Width.IsAbsolute -and $grid.ColumnDefinitions[1].Width.IsStar) {
        $rows=@($grid.Children | ForEach-Object { [Windows.Controls.Grid]::GetRow($_) } | Sort-Object -Unique)
        foreach($row in $rows) {
            $labels=@($grid.Children | Where-Object { [Windows.Controls.Grid]::GetRow($_) -eq $row -and [Windows.Controls.Grid]::GetColumn($_) -eq 0 -and $_ -is [Windows.Controls.TextBlock] })
            $fields=@($grid.Children | Where-Object { [Windows.Controls.Grid]::GetRow($_) -eq $row -and [Windows.Controls.Grid]::GetColumn($_) -eq 1 })
            if($labels.Count -eq 1 -and $fields.Count -ge 1) { New-Object PSObject -Property @{ Label=$labels[0]; Field=$fields[0] } }
        }
    }
    foreach($child in @([Windows.LogicalTreeHelper]::GetChildren($node))) { if($child -is [Windows.DependencyObject]) { FormLabelRows $child } }
}
foreach($section in @('general','email','security')) {
    [void](Call $windowType 'SelectSection' $window @($section))
    Layout 1280 850
    $viewport=$content.FindName('settingsScroll')
    $index=0
    foreach($row in @(FormLabelRows $content)) {
        $label=$row.Label
        if(-not $label.IsVisible) { continue }
        $entry=FirstEntry $row.Field
        if($section -eq 'email' -and $row.Field -is [Windows.Controls.CheckBox]) { $entry=$row.Field }
        if($section -eq 'email' -and $row.Field.Name -eq 'emailReceiveOptions') { $entry=$content.FindName('chkEmailNotify') }
        if($null -eq $entry -or -not $entry.IsVisible) { continue }
        # Skip rows whose field cell opens with something other than the input
        # (for example the keyword-tag strip), where the label pairs with that block.
        $entryTop=$entry.TranslatePoint((New-Object Windows.Point(0,0)),$viewport).Y
        $cellTop=$row.Field.TranslatePoint((New-Object Windows.Point(0,0)),$viewport).Y
        if($entryTop-$cellTop -gt 12) { continue }
        $index++
        $tag='alignment:label-'+$section+'-'+$index
        Assert ($tag+'-no-fixed-height') ([double]::IsNaN($label.Height))
        Assert ($tag+'-no-local-padding') ($label.Padding.Left -eq 0 -and $label.Padding.Top -eq 0 -and $label.Padding.Right -eq 0 -and $label.Padding.Bottom -eq 0)
        Assert ($tag+'-centred-with-input') ([Math]::Abs((LabelTextCentre $label $viewport)-(EntryCentre $entry $viewport)) -le 3)
    }
}
$dialogType=$asm.GetType('Codepass.CodepassDialog')
$guide=Call $dialogType 'CreateWebhookGuide' $null @($window,'http://192.0.2.51:8787/sms')
$guide.ShowActivated=$false;$guide.Show();Pump
$guide.Content.Opacity=1;$guide.Content.UpdateLayout()
$address=[Windows.LogicalTreeHelper]::FindLogicalNode($guide,'webhookEndpoint')
Assert 'webhook:address-is-single-line-selectable' ($address.IsReadOnly -and $address.TextWrapping -eq [Windows.TextWrapping]::NoWrap)
Assert 'webhook:address-fits-without-horizontal-scroll' ($address.ExtentWidth -le $address.ViewportWidth+1)
if([LayoutCapture]::Save($guide.Content,[int]$guide.ActualWidth,[int]$guide.ActualHeight,(Join-Path $evidence 'webhook-aligned.png'))) { Write-Output 'EVIDENCE_SAVED=webhook-aligned.png' }
else { Write-Output 'EVIDENCE_UNAVAILABLE=webhook-aligned.png' }
$guide.Close()
[void](Call $windowType 'SelectSection' $window @('about'))
Layout 780 560
Assert 'layout:diagnostics-action-within-parent' (WithinParent ($content.FindName('btnDiagnostics')))
Capture 'about-narrow.png' 780 560
[void](Call $windowType 'SelectSection' $window @('history'))
Capture 'history-small.png' 640 460
$historyViewport=Call $windowType 'get_HistoryScroll' $window @()
Assert 'history:small-retention-input-and-hint-visible' ($content.FindName('txtHistoryRetention').IsVisible -and $content.FindName('historyRetentionHint').IsVisible -and $null -ne $historyViewport -and $historyViewport.ActualHeight -ge 60)
$retentionHint=$content.FindName('historyRetentionHint')
$hintBottom=$retentionHint.TranslatePoint((New-Object Windows.Point(0,$retentionHint.ActualHeight)),$content).Y
Assert 'history:small-retention-hint-inside-window' ($hintBottom -lt $content.ActualHeight)
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
$tokenReject=[string]([char]0x62D2)+[char]0x7EDD+': token '+[char]0x4E0D+[char]0x5339+[char]0x914D
[void](Call $diagnosticsType 'ObserveLog' $null @('[http] '+$tokenReject))
[void](Call $diagnosticsType 'ObserveLog' $null @('[http] request rejected: body too large'))
$rejectReport=Call $diagnosticsType 'CreateReport' $null @($cfg)
$oversizeLabel=Call $diagnosticsType 'EventCategory' $null @('[http] request rejected: body too large')
Assert 'diagnostics:token-reject-classified' ($null -ne (Call $diagnosticsType 'EventCategory' $null @('[http] '+$tokenReject)))
Assert 'diagnostics:oversize-reject-in-report' (-not [string]::IsNullOrEmpty($oversizeLabel) -and $rejectReport.Contains($oversizeLabel))
Assert 'diagnostics:unrelated-http-log-unclassified' ($null -eq (Call $diagnosticsType 'EventCategory' $null @('[http] client error: '+$synthetic)))
Assert 'diagnostics:body-reject-still-classified' (-not [string]::IsNullOrEmpty($oversizeLabel))
Assert 'diagnostics:header-reject-not-classified' ($null -eq (Call $diagnosticsType 'EventCategory' $null @('[http] request rejected: header too large')))
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
# ---- 顶栏按钮状态、命中测试与窗口位置记忆 ----
$maximizeLabel=[string]([char]0x6700)+[char]0x5927+[char]0x5316
$restoreLabel=[string]([char]0x8FD8)+[char]0x539F
$maximizeGlyph=([char]0xE922).ToString(); $restoreGlyph=([char]0xE923).ToString()
$maxButton=$content.FindName('btnMax'); $minButton=$content.FindName('btnMin'); $closeButton=$content.FindName('btnClose')
Assert 'caption:glyph-follows-state' ((Call $windowType 'CaptionGlyphFor' $null @($false)) -ceq $maximizeGlyph -and (Call $windowType 'CaptionGlyphFor' $null @($true)) -ceq $restoreGlyph)
Assert 'caption:label-follows-state' ((Call $windowType 'CaptionLabelFor' $null @($false)) -eq $maximizeLabel -and (Call $windowType 'CaptionLabelFor' $null @($true)) -eq $restoreLabel)
Assert 'caption:hit-caption-button-stays-client' ((Call $windowType 'CaptionHitCode' $null @($true,$true)) -eq 0)
Assert 'caption:hit-title-bar' ((Call $windowType 'CaptionHitCode' $null @($true,$false)) -eq 2)
Assert 'caption:hit-outside-title-bar' ((Call $windowType 'CaptionHitCode' $null @($false,$false)) -eq 0)
Assert 'caption:buttons-start-at-maximize' ($maxButton.Content -ceq $maximizeGlyph -and $maxButton.ToolTip -eq $maximizeLabel -and $minButton.ToolTip.Length -gt 0 -and $closeButton.ToolTip.Length -gt 0)
[void](Call $windowType 'ToggleMaximize' $window @()); Pump
Assert 'caption:maximized-shows-restore' ($window.WindowState -eq [Windows.WindowState]::Maximized -and $maxButton.Content -ceq $restoreGlyph -and $maxButton.ToolTip -eq $restoreLabel -and [Windows.Automation.AutomationProperties]::GetName($maxButton) -eq $restoreLabel)
[void](Call $windowType 'ToggleMaximize' $window @()); Pump
Assert 'caption:restored-shows-maximize' ($window.WindowState -eq [Windows.WindowState]::Normal -and $maxButton.Content -ceq $maximizeGlyph -and [Windows.Automation.AutomationProperties]::GetName($maxButton) -eq $maximizeLabel)
$maxPoint=$maxButton.TranslatePoint((New-Object Windows.Point(5,15)),$window)
$closePoint=$closeButton.TranslatePoint((New-Object Windows.Point(5,15)),$window)
# 锁屏时遮罩拥有输入：顶栏不参与命中测试（上一段诊断断言把窗口留在锁定态）
$lockOverlay=$content.FindName('lockOverlay')
$lockOverlay.Visibility=[Windows.Visibility]::Visible
Assert 'caption:locked-window-has-no-caption-hit' ((Call $windowType 'CaptionHitFor' $window @((New-Object Windows.Point(60,30)))) -eq 0)
$lockOverlay.Visibility=[Windows.Visibility]::Collapsed
Assert 'caption:title-bar-area-is-caption' ((Call $windowType 'CaptionHitFor' $window @((New-Object Windows.Point(60,30)))) -eq 2)
Assert 'caption:max-button-area-stays-client' ((Call $windowType 'CaptionHitFor' $window @((New-Object Windows.Point($maxPoint.X,$maxPoint.Y)))) -eq 0)
Assert 'caption:close-button-area-is-client' ((Call $windowType 'CaptionHitFor' $window @((New-Object Windows.Point($closePoint.X,$closePoint.Y)))) -eq 0)
Assert 'caption:content-area-untouched' ((Call $windowType 'CaptionHitFor' $window @((New-Object Windows.Point(60,400)))) -eq 0)
$positionPath=Join-Path $root 'window-position.ini'
$positionConfig=[Activator]::CreateInstance($configType,$true)
$positionConfig.WindowWidth=1100; $positionConfig.WindowHeight=760
$positionConfig.Save($positionPath)
Assert 'window:unmeasured-position-not-written' (-not [IO.File]::ReadAllText($positionPath).Contains('window_left'))
$positionConfig.WindowLeft=321; $positionConfig.WindowTop=123; $positionConfig.WindowPositionIncluded=$true
$positionConfig.Save($positionPath)
$positionText=[IO.File]::ReadAllText($positionPath)
$reloadedPosition=Call $configType 'Load' $null @($positionPath)
Assert 'window:position-round-trips' ($positionText.Contains('window_left = 321') -and $positionText.Contains('window_top = 123') -and $reloadedPosition.WindowPositionIncluded -and $reloadedPosition.WindowLeft -eq 321 -and $reloadedPosition.WindowTop -eq 123)
[IO.File]::WriteAllText($positionPath,('port = 8787'+[Environment]::NewLine+'min_len = 4'+[Environment]::NewLine+'max_len = 8'+[Environment]::NewLine+'window_left = 10'+[Environment]::NewLine))
$partialPosition=Call $configType 'Load' $null @($positionPath)
Assert 'window:single-key-is-not-a-position' (-not $partialPosition.WindowPositionIncluded)
$cfg.WindowLeft=64; $cfg.WindowTop=48; $cfg.WindowPositionIncluded=$true
[void](Call $windowType 'ApplyConfiguredWindowPosition' $window @())
Assert 'window:saved-position-restored' ($window.Left -eq 64 -and $window.Top -eq 48)
$cfg.WindowLeft=100000; $cfg.WindowTop=-100000
[void](Call $windowType 'ApplyConfiguredWindowPosition' $window @())
Assert 'window:absurd-position-clamped-into-screen' ($window.Left -lt 100000 -and $window.Top -gt -100000 -and $window.Left -le ([Windows.SystemParameters]::VirtualScreenLeft+[Windows.SystemParameters]::VirtualScreenWidth) -and $window.Top -ge [Windows.SystemParameters]::VirtualScreenTop)
# Restore the persisted synthetic config before exit to avoid saving injected invalid fields.
$restored=Call $configType 'Load' $null @($configPath)
[void](Call $configType 'CopyFrom' $cfg @($restored))
[void](Call $windowType 'LoadValues' $window @())
[void](Call $windowType 'ExitApp' $window @())
Write-Output ('FIXTURES='+$root)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0) { exit 1 }
