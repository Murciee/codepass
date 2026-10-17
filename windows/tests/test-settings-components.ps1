# Isolated config contracts and an email component with an injected fake probe.
# No live network, startup registration, credentials or clipboard access.
param([string]$ExePath, [string]$FixtureDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type -TypeDefinition @'
using System;
using System.Reflection;
using System.Threading;
public static class SyntheticEmailPageProbe {
    public static readonly ManualResetEvent Started = new ManualResetEvent(false);
    public static readonly ManualResetEvent Released = new ManualResetEvent(false);
    public static volatile bool Present = true;
    public static volatile bool WasCancelled;
    public static int Saves;
    public static string LastSection;
    static object identity;
    public static T Read<T>() { return (T)identity; }
    public static string Test<T>(T config, CancellationToken cancel) {
        Started.Set();
        if (!Released.WaitOne(5000)) throw new TimeoutException("Synthetic probe not released");
        WasCancelled = cancel.IsCancellationRequested;
        return "fixture-success";
    }
    public static void Saved<T>(T section) { Saves++; LastSection = section.ToString(); }
    public static object Create(Type page, Type config, Type section, object root, object settings) {
        identity = settings;
        Type probe = typeof(SyntheticEmailPageProbe);
        Delegate read = Delegate.CreateDelegate(typeof(Func<>).MakeGenericType(config), probe.GetMethod("Read").MakeGenericMethod(config));
        Delegate test = Delegate.CreateDelegate(typeof(Func<,,>).MakeGenericType(config, typeof(CancellationToken), typeof(string)), probe.GetMethod("Test").MakeGenericMethod(config));
        object instance = Activator.CreateInstance(page, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
            new object[] { root, settings, new Func<bool>(delegate { return Present; }), read, test }, null);
        Delegate save = Delegate.CreateDelegate(typeof(Action<>).MakeGenericType(section), probe.GetMethod("Saved").MakeGenericMethod(section));
        page.GetEvent("SaveRequested", BindingFlags.Instance | BindingFlags.NonPublic).GetAddMethod(true).Invoke(instance, new object[] { save });
        return instance;
    }
}
'@
$asm = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$config = $asm.GetType('Codepass.Config')
$section = $asm.GetType('Codepass.SettingsSection')
$commit = $asm.GetType('Codepass.SettingsCommit')
$pageType = $asm.GetType('Codepass.EmailSettingsPage')
$windowType = $asm.GetType('Codepass.MainWindow')
$root = [IO.Path]::GetFullPath($FixtureDirectory)
New-Item -ItemType Directory -Path $root | Out-Null
$pass = 0; $fail = 0
function Assert([string]$name, [bool]$ok) {
    if ($ok) { $script:pass++; Write-Output ('PASS ' + $name) }
    else { $script:fail++; Write-Output ('FAIL ' + $name) }
}
function Call($type, [string]$name, $target, [object[]]$values) {
    $parameters = New-Object object[] $values.Length
    for ($i = 0; $i -lt $values.Length; $i++) {
        if ($null -ne $values[$i]) { $parameters[$i] = $values[$i].PSObject.BaseObject }
    }
    if ($null -ne $target) { $target = $target.PSObject.BaseObject }
    $method = $type.GetMethods($flags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Length -eq $values.Length } | Select-Object -First 1
    return ,($method.Invoke($target, $parameters))
}
function NewConfig { return [Activator]::CreateInstance($config, $true) }
function Rejected([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
function Pump {
    $frame = New-Object Windows.Threading.DispatcherFrame
    [void][Windows.Threading.Dispatcher]::CurrentDispatcher.BeginInvoke([Windows.Threading.DispatcherPriority]::ContextIdle, [Action]{$frame.Continue = $false})
    [Windows.Threading.Dispatcher]::PushFrame($frame)
}
function Click([string]$name) { $view.FindName($name).RaiseEvent((New-Object Windows.RoutedEventArgs([Windows.Controls.Button]::ClickEvent))) }
function WaitProbe {
    $deadline = [DateTime]::UtcNow.AddSeconds(4)
    do { Pump; Start-Sleep -Milliseconds 10 } while ($null -ne $pageType.GetField('emailTestCancel', $flags).GetValue($page) -and [DateTime]::UtcNow -lt $deadline)
    Assert 'page:probe-completes' ($null -eq $pageType.GetField('emailTestCancel', $flags).GetValue($page))
}
function StartProbe {
    [void][SyntheticEmailPageProbe]::Started.Reset()
    [void][SyntheticEmailPageProbe]::Released.Reset()
    [SyntheticEmailPageProbe]::WasCancelled = $false
    Click 'btnEmailTest'
    Assert 'page:injected-probe-starts' ([SyntheticEmailPageProbe]::Started.WaitOne(2000))
}

$defaults = NewConfig
Assert 'config:notification-defaults' ($defaults.ShowTip -and $defaults.EmailNotify -and $defaults.EmailAutoCopy)
Assert 'config:interval-default' ($defaults.EmailPollSeconds -eq 30)
$expected = [ordered]@{
    Port = 8901; NtfyServer = 'https://fixture.invalid'; FilterKeywords = "order`nshipped"; FilterRegex = '^fixture$'
    SmsAcceptKeywords = "otp`ncode"; MinLen = 5; MaxLen = 9; SmsMaxLength = 650; AutoClearSeconds = 45
    ShowTip = $false; StartWithWindows = $true; HistoryRetentionMinutes = 25; PrivacyMode = 1; EmailPrivacyMode = 2
    PcIp = '192.0.2.10'; EmailEnabled = $true; EmailServer = 'imap.fixture.invalid'; EmailAccount = 'fixture@example.invalid'
    EmailPollSeconds = 90; EmailKeywords = "alpha`nbeta"; EmailSenders = 'example.invalid'; EmailCheckJunk = $true
    EmailNotify = $false; EmailAutoCopy = $false; EmailSaveBody = $true; QqPopupEnabled = $true
}
$original = NewConfig
foreach ($name in $expected.Keys) { $original.$name = $expected[$name] }
$path = Join-Path $root 'settings.ini'; $original.Save($path)
$loaded = Call $config 'Load' $null @($path, $true)
$copied = NewConfig; $copied.CopyFrom($original)
foreach ($name in $expected.Keys) {
    Assert ('config:roundtrip-' + $name) ($loaded.$name -ceq $expected[$name])
    Assert ('config:copy-' + $name) ($copied.$name -ceq $expected[$name])
}
$target = NewConfig
$target.EmailMailboxState = 'latest-watermark'; $target.EmailLastUid = 81
$original.EmailMailboxState = 'stale-watermark'; $original.EmailLastUid = 12
[void](Call $commit 'Merge' $null @($target, $original, [Enum]::Parse($section, 'EmailRules')))
Assert 'config:rules-merge' ($target.EmailPollSeconds -eq 90 -and $target.EmailCheckJunk -and $target.EmailSaveBody -and $target.QqPopupEnabled -and -not $target.EmailNotify)
Assert 'config:rules-preserve-connection' ($target.EmailServer -eq 'imap.qq.com' -and -not $target.EmailEnabled)
Assert 'config:rules-preserve-other-section' ($target.Port -eq 8787 -and $target.HistoryRetentionMinutes -eq 10)
Assert 'config:rules-preserve-watermark' ($target.EmailMailboxState -eq 'latest-watermark' -and $target.EmailLastUid -eq 81)
$legacy = Join-Path $root 'legacy.ini'
$required = "port=8787`nmin_len=4`nmax_len=8`n"
[IO.File]::WriteAllText($legacy, $required + "email_poll_seconds=65`nemail_notify=0`nemail_keywords_b64=YWxwaGEKYmV0YQ==`nqq_popup_enabled=true`n")
$legacyConfig = Call $config 'Load' $null @($legacy, $true)
Assert 'config:legacy-keys' ($legacyConfig.EmailPollSeconds -eq 65 -and -not $legacyConfig.EmailNotify -and $legacyConfig.QqPopupEnabled -and $legacyConfig.EmailKeywords -eq "alpha`nbeta")
foreach ($line in @('email_notify=invalid', 'email_keywords_b64=invalid!')) {
    [IO.File]::WriteAllText($legacy, $required + $line)
    Assert ('config:strict-' + $line.Split('=')[0]) (Rejected { Call $config 'Load' $null @($legacy, $true) })
}
[IO.File]::WriteAllText($legacy, $required + 'email_poll_seconds=601')
Assert 'config:legacy-invalid-interval-default' ((Call $config 'Load' $null @($legacy)).EmailPollSeconds -eq 30)

# Construct the component without MainWindow, Program or a real IMAP implementation.
$view = [Windows.Markup.XamlReader]::Parse($windowType.GetField('UiXaml', $flags).GetRawConstantValue())
$settings = NewConfig
$settings.EmailAccount = 'fixture@example.invalid'; $settings.EmailAuth = 'synthetic-auth'
$page = [SyntheticEmailPageProbe]::Create($pageType, $config, $section, $view, $settings)
[void](Call $pageType 'Load' $page @())
Assert 'page:load-does-not-save' ([SyntheticEmailPageProbe]::Saves -eq 0)
Assert 'page:load-clean-draft' (-not $pageType.GetProperty('ConnectionDirty', $flags).GetValue($page, $null))
Assert 'page:load-poll-value' ($view.FindName('txtEmailPoll').Text -eq '30')
Assert 'page:range-hint' ($view.FindName('emailPollHint').Text.Contains('10' + [char]0xFF5E + '600'))
$view.FindName('txtEmailAccount').Text = 'draft@example.invalid'
Assert 'page:connection-edit-is-local' ($settings.EmailAccount -eq 'fixture@example.invalid' -and [SyntheticEmailPageProbe]::Saves -eq 0)
Assert 'page:connection-dirty' ($pageType.GetProperty('ConnectionDirty', $flags).GetValue($page, $null))
Click 'btnEmailSave'
Assert 'page:connection-explicit-save-event' ([SyntheticEmailPageProbe]::Saves -eq 1 -and [SyntheticEmailPageProbe]::LastSection -eq 'Email')
Click 'chkEmailNotify'
Assert 'page:rules-save-event' ([SyntheticEmailPageProbe]::Saves -eq 2 -and [SyntheticEmailPageProbe]::LastSection -eq 'EmailRules')
[void](Call $pageType 'Load' $page @())
Assert 'page:reload-does-not-save' ([SyntheticEmailPageProbe]::Saves -eq 2)
$invalid = [Func[Windows.FrameworkElement,string,bool]] { param($field,$message) return $false }
$rules = [Enum]::Parse($section, 'EmailRules')
foreach ($value in @('9','601','bad')) {
    $view.FindName('txtEmailPoll').Text = $value
    Assert ('page:reject-interval-' + $value) (-not (Call $pageType 'ReadDraft' $page @($rules, (NewConfig), $invalid)))
    Assert ('page:retain-invalid-' + $value) ($view.FindName('txtEmailPoll').Text -eq $value -and $settings.EmailPollSeconds -eq 30)
}
foreach ($value in @('10','600')) {
    $view.FindName('txtEmailPoll').Text = $value
    $draft = NewConfig
    Assert ('page:accept-boundary-' + $value) (Call $pageType 'ReadDraft' $page @($rules, $draft, $invalid))
    Assert ('page:read-boundary-' + $value) ($draft.EmailPollSeconds -eq [int]$value)
}
[void](Call $pageType 'Load' $page @())
StartProbe
$view.FindName('txtEmailAccount').Text = 'new-draft@example.invalid'
$newDraftNotice = $view.FindName('lblEmailTestResult').Text
[void][SyntheticEmailPageProbe]::Released.Set(); WaitProbe
Assert 'page:edit-cancels-probe' ([SyntheticEmailPageProbe]::WasCancelled)
Assert 'page:late-result-preserves-new-draft' ($view.FindName('lblEmailTestResult').Text -eq $newDraftNotice)
[void](Call $pageType 'Load' $page @())
StartProbe
Click 'btnEmailTest'
$cancelNotice = $view.FindName('lblEmailTestResult').Text
[void][SyntheticEmailPageProbe]::Released.Set(); WaitProbe
Assert 'page:cancel-button-cancels-probe' ([SyntheticEmailPageProbe]::WasCancelled)
Assert 'page:cancel-result-not-overwritten' ($view.FindName('lblEmailTestResult').Text -eq $cancelNotice)
StartProbe
$settings.EmailServer = 'imap.163.com'
[void](Call $pageType 'Load' $page @())
Assert 'page:reload-clears-probe-status' ($view.FindName('lblEmailTestResult').Text.Length -eq 0)
Assert 'page:reload-updates-fields' ($view.FindName('txtEmailServer').Text -eq 'imap.163.com')
[void][SyntheticEmailPageProbe]::Released.Set(); WaitProbe
Assert 'page:reload-cancels-probe' ([SyntheticEmailPageProbe]::WasCancelled)
Assert 'page:reload-result-stays-clear' ($view.FindName('lblEmailTestResult').Text.Length -eq 0)
StartProbe
[SyntheticEmailPageProbe]::Present = $false
$hiddenNotice = $view.FindName('lblEmailTestResult').Text
[void][SyntheticEmailPageProbe]::Released.Set(); WaitProbe
Assert 'page:hidden-probe-not-cancelled' (-not [SyntheticEmailPageProbe]::WasCancelled)
Assert 'page:hidden-result-suppressed' ($view.FindName('lblEmailTestResult').Text -eq $hiddenNotice)
[SyntheticEmailPageProbe]::Present = $true
StartProbe
[void][SyntheticEmailPageProbe]::Released.Set(); WaitProbe
Assert 'page:current-result-shown' ($view.FindName('lblEmailTestResult').Text.Contains('fixture-success'))
Assert 'page:test-does-not-save' ([SyntheticEmailPageProbe]::Saves -eq 2)
Write-Output ('TOTAL pass=' + $pass + ' fail=' + $fail)
if ($fail -gt 0) { exit 1 }
