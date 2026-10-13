# Synthetic IMAP, config and history only; no live mail or clipboard access.
param([string]$ExePath = (Join-Path $PSScriptRoot '..\dist\codepass.exe'), [string]$FixtureDirectory = '', [string]$EvidenceDirectory = '')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName PresentationFramework,PresentationCore,WindowsBase,System.Xaml
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;
public sealed class SecurityMailReplay : Stream {
    readonly MemoryStream input;
    readonly MemoryStream output = new MemoryStream();
    public SecurityMailReplay(string data) { input = new MemoryStream(Encoding.UTF8.GetBytes(data)); }
    public string Written { get { return Encoding.UTF8.GetString(output.ToArray()); } }
    public override bool CanRead { get { return true; } }
    public override bool CanWrite { get { return true; } }
    public override bool CanSeek { get { return false; } }
    public override long Length { get { return input.Length; } }
    public override long Position { get { return input.Position; } set { throw new NotSupportedException(); } }
    public override int ReadTimeout { get; set; }
    public override int WriteTimeout { get; set; }
    public bool ThrowOnceOnFetch { get; set; }
    public override int Read(byte[] b,int o,int n) {
        if (ThrowOnceOnFetch && Written.Contains("UID FETCH")) { ThrowOnceOnFetch = false; throw new InvalidOperationException("Synthetic unexpected fetch failure"); }
        return input.Read(b,o,n);
    }
    public override void Write(byte[] b,int o,int n) { output.Write(b,o,n); }
    public override void Flush() { }
    public override long Seek(long o,SeekOrigin s) { throw new NotSupportedException(); }
    public override void SetLength(long n) { throw new NotSupportedException(); }
}
'@
Add-Type -ReferencedAssemblies @([Windows.Window].Assembly.Location,[Windows.UIElement].Assembly.Location,[Windows.Threading.Dispatcher].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location) -TypeDefinition @'
using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
public static class HistoryClickProbe {
    public static bool DoubleClickAndClose(Window owner, UIElement target, string expected) {
        bool observed = false;
        DispatcherTimer timer = new DispatcherTimer();
        timer.Interval = TimeSpan.FromMilliseconds(50);
        timer.Tick += delegate {
            foreach (Window dialog in owner.OwnedWindows) {
                Grid body = dialog.Content as Grid;
                if (body == null || body.Children.Count < 2) continue;
                TextBox text = body.Children[1] as TextBox;
                observed = text != null && text.Text == expected;
                timer.Stop();
                dialog.Close();
                break;
            }
        };
        MouseButtonEventArgs args = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left);
        typeof(MouseButtonEventArgs).GetProperty("ClickCount").GetSetMethod(true).Invoke(args, new object[] { 2 });
        args.RoutedEvent = UIElement.MouseLeftButtonDownEvent;
        timer.Start();
        try { target.RaiseEvent(args); }
        finally { timer.Stop(); }
        return observed;
    }
}
'@
$asm=[Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($ExePath))
$flags=[Reflection.BindingFlags]'Public,NonPublic,Instance,Static'
$configType=$asm.GetType('Codepass.Config'); $programType=$asm.GetType('Codepass.Program')
$delivery=$programType.GetField('Incoming',$flags).GetValue($null)
$receipts=$asm.GetType('Codepass.History').GetField('Receipts',$flags).GetValue($null)
$watcher=$asm.GetType('Codepass.EmailWatcher'); $imapType=$asm.GetType('Codepass.Imap')
$mime=$asm.GetType('Codepass.Mime'); $extractor=$asm.GetType('Codepass.CodeExtractor')
$windowType=$asm.GetType('Codepass.MainWindow'); $history=$asm.GetType('Codepass.History')
$itemType=$asm.GetType('Codepass.HistoryItem')
$root=if($FixtureDirectory) { [IO.Path]::GetFullPath($FixtureDirectory) } else { Join-Path $PSScriptRoot ('..\build\security-mail-fixtures-'+[Guid]::NewGuid().ToString('N')) }
New-Item -ItemType Directory -Path $root | Out-Null
$evidence=if($EvidenceDirectory) { [IO.Path]::GetFullPath($EvidenceDirectory) } else { Join-Path $root 'evidence' }
New-Item -ItemType Directory -Force -Path $evidence | Out-Null
$pass=0; $fail=0
function Assert([string]$name,[bool]$ok) { if($ok) { $script:pass++; Write-Output ('PASS '+$name) } else { $script:fail++; Write-Output ('FAIL '+$name) } }
function Call($class,[string]$name,$target,[object[]]$values) {
    $argsArray=New-Object object[] $values.Length
    for($i=0;$i -lt $values.Length;$i++) { if($null -ne $values[$i]) { $argsArray[$i]=$values[$i].PSObject.BaseObject } }
    if($null -ne $target) { $target=$target.PSObject.BaseObject }
    $method=$class.GetMethods($flags) | Where-Object { $_.Name -eq $name -and $_.GetParameters().Count -eq $values.Length } | Select-Object -First 1
    if($null -eq $method) { throw ('Missing test method '+$name) }
    return ,($method.Invoke($target,$argsArray))
}
function Rejected([scriptblock]$action) { try { & $action | Out-Null; return $false } catch { return $true } }
function SaveVisual($visual,[int]$width,[int]$height,[string]$name) {
    $visual.Opacity=1
    if($visual -is [Windows.Controls.Panel] -and $null -eq $visual.Background) {
        $visual.Background=New-Object Windows.Media.SolidColorBrush([Windows.Media.Color]::FromRgb(243,243,243))
    }
    $visual.Measure((New-Object Windows.Size($width,$height)))
    $visual.Arrange((New-Object Windows.Rect(0,0,$width,$height)))
    $visual.UpdateLayout()
    $bitmap=New-Object Windows.Media.Imaging.RenderTargetBitmap($width,$height,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $png=New-Object Windows.Media.Imaging.PngBitmapEncoder
    $png.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $imagePath=Join-Path $evidence $name
    $output=[IO.File]::Create($imagePath)
    try { $png.Save($output) } finally { $output.Dispose() }
}
function Enc([string]$value) { [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($value)) }
function Replay([string]$data) {
    $imap=[Activator]::CreateInstance($imapType,$true); $stream=New-Object SecurityMailReplay($data)
    [void](Call $imapType 'Attach' $imap @($stream)); return @($imap,$stream)
}
function Literal([string]$prefix,[string]$data,[int]$tag) {
    return $prefix+' {'+[Text.Encoding]::UTF8.GetByteCount($data)+"}`r`n"+$data+")`r`nC"+$tag+" OK done`r`n"
}
$security=[string][char]0x5B89+[char]0x5168+[char]0x4EE3+[char]0x7801
$subject='Microsoft '+[char]0x8D26+[char]0x6237+' '+$security
$plain='Microsoft account access: visit the account website. Reference 987654.'
$html='<html><body><p>Microsoft '+$security+'</p><p>'+[char]0x8BF7+[char]0x4F7F+[char]0x7528+[char]0x4EE5+[char]0x4E0B+$security+'</p><div>'+$security+': <b>654321</b></div><p>Reference: 123456</p><p>'+('fixture text '*150)+'</p></body></html>'
$plainStruct='("TEXT" "PLAIN" ("CHARSET" "UTF-8") NIL NIL "BASE64" 100 2 NIL NIL NIL)'
$htmlStruct='("TEXT" "HTML" ("CHARSET" "UTF-8") NIL NIL "BASE64" 4096 2 NIL NIL NIL)'
$attachStruct='("TEXT" "PLAIN" ("CHARSET" "UTF-8" "NAME" "old.txt") NIL NIL "BASE64" 100 2 NIL ("ATTACHMENT" ("FILENAME" "old.txt")) NIL)'
$structure='('+$plainStruct+' '+$htmlStruct+' "ALTERNATIVE" NIL NIL)'
$now=[DateTimeOffset]::UtcNow
function MailWire([int]$startTag,[long]$uid,[string]$messageId,[DateTimeOffset]$received) {
    $date=$received.ToString('dd-MMM-yyyy HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture)+' +0000'
    $header='Subject: =?UTF-8?B?'+(Enc $subject)+"?=`r`nFrom: Microsoft <noreply@example.com>`r`nMessage-ID: <"+$messageId+">`r`n"
    $wire=Literal ('* 1 FETCH (UID '+$uid+' INTERNALDATE "'+$date+'" BODY[HEADER.FIELDS (SUBJECT FROM DATE MESSAGE-ID)]') $header $startTag
    $wire+='* 1 FETCH (UID '+$uid+' BODYSTRUCTURE '+$structure+")`r`nC"+($startTag+1)+" OK structure`r`n"
    $wire+=Literal ('* 1 FETCH (UID '+$uid+' BODY[1]<0>') (Enc $plain) ($startTag+2)
    $wire+=Literal ('* 1 FETCH (UID '+$uid+' BODY[2]<0>') (Enc $html) ($startTag+3)
    return $wire
}
$cfg=[Activator]::CreateInstance($configType,$true)
$cfg.EmailServer='imap.qq.com'; $cfg.EmailAccount='tester@example.com'; $cfg.EmailAuth='synthetic-fixture'
$cfg.EmailEnabled=$true; $cfg.EmailNotify=$false; $cfg.EmailAutoCopy=$false; $cfg.EmailSaveBody=$true
$cfg.QqPopupEnabled=$false; $cfg.PcIp='192.0.2.1'
$programType.GetField('cfg',$flags).SetValue($null,$cfg)
$programType.GetField('appDir',$flags).SetValue($null,[string]$root)
$gen=Call $watcher 'BeginMainGeneration' $null @()
$mailbox=[Activator]::CreateInstance($asm.GetType('Codepass.Mailbox'),$true)
$mailbox.Name='INBOX'; $mailbox.UidValidity=7; $mailbox.UidNext=31
$historyPath=Join-Path $root 'history.txt'
[void](Call $history 'Init' $null @($historyPath))
Assert 'default:security-code-keyword' ($cfg.EmailKeywords.Contains($security))
Assert 'extract:security-explicit' ((Call $extractor 'ExtractExplicit' $null @(($security+': 654321'),4,8)) -eq '654321')
Assert 'extract:security-short-code-context' ((Call $extractor 'Extract' $null @(($security+': 4321 reference 987654'),4,8)) -eq '4321')
Assert 'extract:long-code-not-prefix' ($null -eq (Call $extractor 'ExtractExplicit' $null @(($security+': 12345678901'),4,8)))
Assert 'extract:date-year-not-fallback' ($null -eq (Call $extractor 'Extract' $null @('Valid until 2027.01.12',4,8)))
Assert 'extract:date-year-suffix-chinese' ($null -eq (Call $extractor 'Extract' $null @(('expires 2027'+[char]0x5E74+'01'),4,8)))
Assert 'extract:plain-number-still-fallback' ((Call $extractor 'Extract' $null @('reference 2027 shipped',4,8)) -eq '2027')
Assert 'extract:context-beats-date-guard' ((Call $extractor 'Extract' $null @('verification code 1234 effective 2027.01.12',4,8)) -eq '1234')
Assert 'extract:decimal-amount-not-fallback' ($null -eq (Call $extractor 'Extract' $null @('paid 1234.56 total',4,8)))
Assert 'extract:fallback-prefers-six-digit' ((Call $extractor 'Extract' $null @('ref 4321 and 987654',4,8)) -eq '987654')
Assert 'extract:fallback-takes-last' ((Call $extractor 'Extract' $null @('ref 4321 and 8765',4,8)) -eq '8765')
$pair=Replay ('* 1 FETCH (UID 25 BODYSTRUCTURE '+$structure+")`r`nC1 OK structure`r`n"+(Literal '* 1 FETCH (UID 25 BODY[1]<0>' (Enc $plain) 2)+(Literal '* 1 FETCH (UID 25 BODY[2]<0>' (Enc $html) 3))
$text=Call $imapType 'FetchTextForCode' $pair[0] @([long]25,4,8)
Assert 'body:plain-reference-does-not-stop-html' ($pair[1].Written.Contains('BODY.PEEK[2]'))
Assert 'body:html-security-code-selected' ((Call $extractor 'ExtractExplicit' $null @($text,4,8)) -eq '654321')
Assert 'body:no-html-markup' (-not $text.Contains('<html>'))
$pair=Replay (MailWire 1 25 'new-synthetic@example.com' $now)
[void](Call $watcher 'ProcessOne' $null @($pair[0],[long]25,$gen,$false,$cfg,$mailbox,$false))
$items=Call $history 'Snapshot' $null @()
Assert 'endtoend:security-mail-recorded' ($items.Count -eq 1 -and $items[0].Code -eq '654321')
Assert 'endtoend:real-sender-stored' ($items[0].Source -eq 'noreply@example.com')
Assert 'history:full-saved-body-not-1000-truncated' ($items[0].Raw.Length -gt 1000 -and $items[0].Raw.EndsWith('fixture text'))
$pair=Replay (MailWire 1 25 'new-synthetic@example.com' $now)
[void](Call $watcher 'ProcessOne' $null @($pair[0],[long]25,$gen,$false,$cfg,$mailbox,$false))
Assert 'history:duplicate-mail-not-recorded' ((Call $history 'Snapshot' $null @()).Count -eq 1)
# Mail already below the watermark must be recoverable after correcting keywords.
$cfg.EmailKeywords='verification code'
$pair=Replay (MailWire 1 26 'recheck-synthetic@example.com' $now)
[void](Call $watcher 'ProcessOne' $null @($pair[0],[long]26,$gen,$false,$cfg,$mailbox,$false))
Assert 'filter:keyword-miss-does-not-download-body' (-not $pair[1].Written.Contains('BODYSTRUCTURE'))
Assert 'filter:keyword-miss-visible' ($watcher.GetField('LastMailResult',$flags).GetValue($null).Length -gt 0)
$water=Call $watcher 'InitializeWater' $null @($cfg,$mailbox,$gen,$false)
Assert 'recheck:initialized-water-30' ($water -eq 30)
$originalState=$cfg.EmailMailboxState
[void](Call $programType 'SetLatestCode' $null @('112233',$false))
$latestBeforeRecheck=(Call $programType 'GetLatestReceipt' $null @()).Code
$cfg.EmailKeywords=$security
$pair=Replay ("* SEARCH 26`r`nC1 OK search`r`n"+(MailWire 2 26 'recheck-synthetic@example.com' $now))
[void](Call $watcher 'ProcessRecent' $null @($pair[0],$gen,$false,$cfg,$mailbox))
Assert 'recheck:previously-skipped-mail-added' ((Call $history 'Snapshot' $null @()).Count -eq 2)
Assert 'recheck:latest-code-unchanged' ((Call $programType 'GetLatestReceipt' $null @()).Code -eq $latestBeforeRecheck)
Assert 'recheck:water-unchanged' ($cfg.EmailMailboxState -eq $originalState)
Assert 'recheck:no-unacknowledged-receipt-added' ($receipts.GetType().GetField('PendingReceipts',$flags).GetValue($receipts).Count -eq 0)
$pair=Replay ("* SEARCH 26`r`nC1 OK search`r`n"+(MailWire 2 26 'recheck-synthetic@example.com' $now))
[void](Call $watcher 'ProcessRecent' $null @($pair[0],$gen,$false,$cfg,$mailbox))
Assert 'recheck:duplicate-idempotent' ((Call $history 'Snapshot' $null @()).Count -eq 2)
$pair=Replay ("* SEARCH 27`r`nC1 OK search`r`n"+(MailWire 2 27 'stale-synthetic@example.com' $now.AddMinutes(-40)))
[void](Call $watcher 'ProcessRecent' $null @($pair[0],$gen,$false,$cfg,$mailbox))
Assert 'recheck:older-than-30min-skipped' ((Call $history 'Snapshot' $null @()).Count -eq 2)
Assert 'recheck:old-body-not-downloaded' (-not $pair[1].Written.Contains('BODYSTRUCTURE'))
$pair=Replay "C1 OK vanished`r`n"
[void](Call $watcher 'ProcessOne' $null @($pair[0],[long]28,$gen,$false,$cfg,$mailbox,$false))
Assert 'header:vanished-mail-not-stuck' ((Call $history 'Snapshot' $null @()).Count -eq 2)
$pair=Replay "* 1 FETCH (UID 28 FLAGS ())`r`nC1 OK done`r`n"
Assert 'header:incomplete-return-retries' (Rejected { Call $watcher 'ProcessOne' $null @($pair[0],[long]28,$gen,$false,$cfg,$mailbox,$false) })
$pair=Replay ("* SEARCH "+((1..250) -join ' ')+"`r`nC1 OK search`r`n")
$uids=Call $imapType 'SearchRecentUids' $pair[0] @($now.AddMinutes(-30),200)
Assert 'recheck:bounded-latest-200' ($uids.Count -eq 200 -and $uids[0] -eq 51 -and $uids[199] -eq 250)
Assert 'recheck:candidate-total-preserved' ($pair[0].LastRecentCandidateCount -eq 250)

# A malformed BODYSTRUCTURE must not indefinitely block the next UID.
$badHeader='Subject: =?UTF-8?B?'+(Enc $subject)+"?=`r`nFrom: noreply@example.com`r`nMessage-ID: <bad-structure@example.com>`r`n"
$badDate=$now.ToString('dd-MMM-yyyy HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture)+' +0000'
function BadMail([int]$tag,[long]$uid,[string]$completion) {
    return (Literal ('* 1 FETCH (UID '+$uid+' INTERNALDATE "'+$badDate+'" BODY[HEADER.FIELDS (SUBJECT FROM DATE MESSAGE-ID)]') $badHeader $tag)+$completion
}
$badBody='* 1 FETCH (UID 60 BODYSTRUCTURE ("TEXT"'+"`r`nC3 OK malformed`r`n"
for($retry=1;$retry -le 2;$retry++) {
    $pair=Replay ("* SEARCH 60 61`r`nC1 OK search`r`n"+(BadMail 2 60 $badBody))
    Assert ('isolation:first-two-retry-'+$retry) (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]59,$false,$gen,$cfg,$mailbox) })
    Assert ('isolation:later-uid-not-consumed-'+$retry) (-not $pair[1].Written.Contains('UID FETCH 61'))
}
$pair=Replay ("* SEARCH 60 61`r`nC1 OK search`r`n"+(BadMail 2 60 $badBody)+(MailWire 4 61 'after-bad@example.com' $now)+"* SEARCH`r`nC8 OK search`r`n")
$advanced=Call $watcher 'ProcessNew' $null @($pair[0],[long]59,$false,$gen,$cfg,$mailbox)
Assert 'isolation:third-content-error-allows-next-uid' ($advanced -eq 61 -and $pair[1].Written.Contains('UID FETCH 61'))
Assert 'isolation:following-code-recorded' ((Call $history 'Snapshot' $null @())[0].MessageKey.Length -gt 0)

# Server NO and incomplete network responses are never counted as content failures.
for($retry=1;$retry -le 3;$retry++) {
    $pair=Replay ("* SEARCH 70`r`nC1 OK search`r`n"+(BadMail 2 70 "C3 NO temporarily unavailable`r`n"))
    Assert ('isolation:no-never-advances-'+$retry) (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]69,$false,$gen,$cfg,$mailbox) })
}
for($retry=1;$retry -le 3;$retry++) {
    $pair=Replay "* SEARCH 71`r`nC1 OK search`r`n"
    Assert ('isolation:network-never-advances-'+$retry) (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]70,$false,$gen,$cfg,$mailbox) })
}
# Unexpected exceptions cannot advance a watermark or be counted as content errors.
$beforeUnexpected=$cfg.EmailMailboxState
for($retry=1;$retry -le 3;$retry++) {
    $pair=Replay "* SEARCH 72`r`nC1 OK search`r`n"
    $pair[1].ThrowOnceOnFetch=$true
    Assert ('isolation:unexpected-never-advances-'+$retry) (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]71,$false,$gen,$cfg,$mailbox) })
    Assert ('isolation:unexpected-water-preserved-'+$retry) ($cfg.EmailMailboxState -eq $beforeUnexpected)
}
# Invalid Base64 is an explicit body-content failure, with the same three-attempt policy.
$invalidBase64='* 1 FETCH (UID 80 BODYSTRUCTURE '+$plainStruct+" )`r`nC3 OK structure`r`n"+(Literal '* 1 FETCH (UID 80 BODY[1]<0>' '!!!invalid-base64!!!' 4)
for($retry=1;$retry -le 2;$retry++) {
    $pair=Replay ("* SEARCH 80 81`r`nC1 OK search`r`n"+(BadMail 2 80 $invalidBase64))
    Assert ('isolation:base64-first-two-retry-'+$retry) (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]79,$false,$gen,$cfg,$mailbox) })
    Assert ('isolation:base64-following-not-consumed-'+$retry) (-not $pair[1].Written.Contains('UID FETCH 81'))
}
$pair=Replay ("* SEARCH 80 81`r`nC1 OK search`r`n"+(BadMail 2 80 $invalidBase64)+(MailWire 5 81 'after-base64@example.com' $now)+"* SEARCH`r`nC9 OK search`r`n")
$advanced=Call $watcher 'ProcessNew' $null @($pair[0],[long]79,$false,$gen,$cfg,$mailbox)
Assert 'isolation:base64-third-allows-next-uid' ($advanced -eq 81 -and $pair[1].Written.Contains('UID FETCH 81'))
# A transient error breaks a sequence of identical content failures.
$brokenStructure='* 1 FETCH (UID 82 BODYSTRUCTURE ("TEXT"'+"`r`nC3 OK malformed`r`n"
$pair=Replay ("* SEARCH 82`r`nC1 OK search`r`n"+(BadMail 2 82 $brokenStructure))
Assert 'isolation:sequence-first-content-retries' (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]81,$false,$gen,$cfg,$mailbox) })
$pair=Replay ("* SEARCH 82`r`nC1 OK search`r`n"+(BadMail 2 82 "C3 NO transient`r`n"))
Assert 'isolation:sequence-transient-retries' (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]81,$false,$gen,$cfg,$mailbox) })
for($retry=1;$retry -le 2;$retry++) {
    $pair=Replay ("* SEARCH 82`r`nC1 OK search`r`n"+(BadMail 2 82 $brokenStructure))
    Assert ('isolation:sequence-restarts-count-'+$retry) (Rejected { Call $watcher 'ProcessNew' $null @($pair[0],[long]81,$false,$gen,$cfg,$mailbox) })
}
[void](Call $watcher 'SetStatus' $null @($gen,$cfg,'synthetic-current-status'))
[void](Call $watcher 'SetStatus' $null @(($gen-1),$cfg,'synthetic-stale-status'))
Assert 'status:stale-generation-cannot-overwrite' ($watcher.GetField('Status',$flags).GetValue($null) -eq 'synthetic-current-status')
$folderStateBefore=$watcher.GetField('LastFolderResult',$flags).GetValue($null)
$folderGen=Call $watcher 'BeginJunkGeneration' $null @()
[void](Call $watcher 'MailResult' $null @($folderGen,$true,$cfg,'synthetic-folder-result'))
$mainStateBefore=$watcher.GetField('LastMailResult',$flags).GetValue($null)
Assert 'status:folder-result-independent' ($watcher.GetField('LastFolderResult',$flags).GetValue($null) -eq 'synthetic-folder-result')
[void](Call $watcher 'MailResult' $null @($gen,$false,$cfg,'synthetic-main-result'))
Assert 'status:main-does-not-overwrite-folder' ($watcher.GetField('LastFolderResult',$flags).GetValue($null) -eq 'synthetic-folder-result')
$watcher.GetField('LastFolderResult',$flags).SetValue($null,$folderStateBefore)
# Literal after another literal / continuation is legal: associate it with UID.
$utfHeader='Subject: =?UTF-8?B?'+(Enc $subject)+"?=`r`nFrom: noreply@example.com`r`n"
$date=$now.ToString('dd-MMM-yyyy HH:mm:ss',[Globalization.CultureInfo]::InvariantCulture)+' +0000'
$wire='* 1 FETCH (UID 29 BODY[TEXT] {4}'+"`r`n"+'junk'+' BODY[HEADER.FIELDS (SUBJECT FROM DATE MESSAGE-ID)] {'+[Text.Encoding]::UTF8.GetByteCount($utfHeader)+"}`r`n"+$utfHeader+' INTERNALDATE "'+$date+'")'+"`r`nC1 OK header`r`n"
$pair=Replay $wire
$header=Call $imapType 'FetchHeaderDetails' $pair[0] @([long]29)
Assert 'header:continuation-literal-associated' ($header.Found -and $null -ne $header.Data -and $null -ne $header.Received)
$pair=Replay ("* 1 FETCH (UID 999 BODY[1] {4}`r`njunk)`r`n"+(Literal '* 2 FETCH (UID 30 BODY[1]<0>' (Enc $html) 1))
$body=Call $imapType 'FetchLiteral' $pair[0] @([long]30,'BODY.PEEK[1]<0.512000>')
Assert 'fetch:other-uid-literal-not-selected' ([Text.Encoding]::ASCII.GetString($body) -eq (Enc $html))

# Partial retrieval must not pretend to contain the complete original.
$longText=$security+': 654321'+"`n"+('fixture line '+"`n")*2500
$longWire=MailWire 1 40 'truncated@example.com' $now
$firstHeaderEnd=$longWire.IndexOf('C1 OK done')+('C1 OK done' + "`r`n").Length
$longWire=$longWire.Substring(0,$firstHeaderEnd)+'* 1 FETCH (UID 40 BODYSTRUCTURE '+$plainStruct+")`r`nC2 OK structure`r`n"+(Literal '* 1 FETCH (UID 40 BODY[1]<0>' (Enc $longText) 3)
$pair=Replay $longWire
[void](Call $watcher 'ProcessOne' $null @($pair[0],[long]40,$gen,$false,$cfg,$mailbox,$false))
$truncatedItem=(Call $history 'Snapshot' $null @())[0]
Assert 'body:20000-cut-marked' ($truncatedItem.Raw.EndsWith([string][char]0xFF09))
Assert 'body:cut-marker-not-used-as-code' ($truncatedItem.Code -eq '654321')
$largeText=[Text.Encoding]::UTF8.GetBytes(($security+': 654321'+"`n"+('x'*512000)))
$largeData=New-Object byte[] 512000
[Array]::Copy($largeText,$largeData,512000)
# Raw 8BIT section replay uses Latin-1 stream fixture conversion only for ASCII text.
$largePrefix='verification code 654321'+"`n"
$largeAscii=$largePrefix+('x'*(512000-$largePrefix.Length))
$largeStruct='("TEXT" "PLAIN" ("CHARSET" "UTF-8") NIL NIL "8BIT" 600000 2 NIL NIL NIL)'
$pair=Replay ('* 1 FETCH (UID 40 BODYSTRUCTURE '+$largeStruct+")`r`nC1 OK structure`r`n"+(Literal '* 1 FETCH (UID 40 BODY[1]<0>' $largeAscii 2))
[void](Call $imapType 'FetchTextForCode' $pair[0] @([long]40,4,8))
Assert 'body:512kb-cut-flagged' $pair[0].LastTextTruncated

[void](Call $watcher 'RequestRecentCheck' $null @())
Assert 'request:same-account-pending' ((Call $watcher 'PendingRecentRequest' $null @($cfg,$false)) -gt 0)
Assert 'request:junk-wakeup-set' ($watcher.GetField('junkWake',$flags).GetValue($null).IsSet)
$cfg.EmailAccount='other@example.com'
[void](Call $watcher 'RequestStop' $null @())
Assert 'request:other-account-not-inherited' ((Call $watcher 'PendingRecentRequest' $null @($cfg,$false)) -eq 0)
$cfg.EmailAccount='tester@example.com'
[void](Call $watcher 'RequestRecentCheck' $null @())
$cfg.EmailEnabled=$false
[void](Call $watcher 'RequestStop' $null @())
$cfg.EmailEnabled=$true
Assert 'request:disabled-request-canceled' ((Call $watcher 'PendingRecentRequest' $null @($cfg,$false)) -eq 0)
[void](Call $watcher 'RequestRecentCheck' $null @())
[void](Call $watcher 'RequestStop' $null @())
Assert 'request:same-account-restart-preserved' ((Call $watcher 'PendingRecentRequest' $null @($cfg,$false)) -gt 0)

$dedupPath=Join-Path $root 'recent-dedup.txt'
[void](Call $history 'Init' $null @($dedupPath))
for($index=1;$index -le 205;$index++) {
    $record=[Activator]::CreateInstance($itemType,$true)
    $record.Channel='email'; $record.MessageKey='recent-'+$index; $record.Code='654321'
    [void](Call $history 'AddUnique' $null @($record))
}
Assert 'dedup:visible-history-still-100' ((Call $history 'Snapshot' $null @()).Count -eq 100)
Assert 'dedup:recent-independent-205' ($receipts.GetType().GetField('RecentMessages',$flags).GetValue($receipts).Count -eq 205)
[void](Call $history 'Init' $null @($dedupPath))
$duplicate=[Activator]::CreateInstance($itemType,$true); $duplicate.Channel='email'; $duplicate.MessageKey='recent-1'; $duplicate.Code='654321'
Assert 'dedup:evicted-visible-mail-not-readded-after-restart' (-not (Call $history 'AddUnique' $null @($duplicate)))
$repeatAdded=0
for($index=1;$index -le 205;$index++) {
    $duplicate.MessageKey='recent-'+$index
    if(Call $history 'AddUnique' $null @($duplicate)) { $repeatAdded++ }
}
Assert 'dedup:full-repeat-205-no-new-records' ($repeatAdded -eq 0)
$latestPath=Join-Path $root 'latest-code.txt'
[void](Call $history 'Init' $null @($latestPath))
$freshRecord=[Activator]::CreateInstance($itemType,$true); $freshRecord.Channel='sms'; $freshRecord.Code='112233'
[void](Call $history 'AddUnique' $null @($freshRecord))
$cfg.EmailEnabled=$true
[void](Call $delivery.GetType() 'SubmitEmail' $delivery @('synthetic old mail','noreply@example.com','654321','latest-recheck-fixture',$cfg,$false,[Func[bool]]{return $true},$null,[long]0,$false))
Assert 'latest:recheck-flag-stored' (-not (Call $history 'Snapshot' $null @())[0].CanBeLatest)
[void](Call $history 'Init' $null @($latestPath))
Assert 'latest:restart-skips-older-recheck-code' ((Call $history 'LatestCode' $null @()) -eq '112233')
$onlyRecheckPath=Join-Path $root 'only-recheck.txt'
[void](Call $history 'Init' $null @($onlyRecheckPath))
$oldRecord=[Activator]::CreateInstance($itemType,$true); $oldRecord.Channel='email'; $oldRecord.Code='654321'; $oldRecord.CanBeLatest=$false
[void](Call $history 'AddUnique' $null @($oldRecord))
[void](Call $history 'Init' $null @($onlyRecheckPath))
Assert 'latest:recheck-only-no-startup-code' ($null -eq (Call $history 'LatestCode' $null @()))
$legacyHistoryPath=Join-Path $root 'legacy-latest.txt'
[IO.File]::WriteAllText($legacyHistoryPath,"10:00`tSynthetic`t998877`toriginal text")
[void](Call $history 'Init' $null @($legacyHistoryPath))
Assert 'latest:legacy-history-compatible' ((Call $history 'LatestCode' $null @()) -eq '998877')
[void](Call $history 'Init' $null @($historyPath))

$folderWire="* LIST () `"/`" `"INBOX`"`r`n* LIST () `"/`" `"CategoryA`"`r`n* LIST (\Junk) `"/`" `"Spam`"`r`n* LIST (\Sent) `"/`" `"MailSent`"`r`n* LIST (\Drafts) `"/`" `"DraftWork`"`r`n* LIST (\Trash) `"/`" `"DeletedWork`"`r`n* LIST (\All) `"/`" `"AllWork`"`r`n* LIST () `"/`" `"Sent/Archive`"`r`n* LIST (\Noselect) `"/`" `"Parent`"`r`nC1 OK folders`r`n"
$pair=Replay $folderWire
$folderDetails=Call $imapType 'ListFolderDetails' $pair[0] @()
$receiving=Call $watcher 'ReceiveFolders' $null @($folderDetails,$false)
Assert 'folders:category-monitored-with-junk-off' ($receiving.Count -eq 1 -and $receiving[0].Name -eq 'CategoryA')
$receiving=Call $watcher 'ReceiveFolders' $null @($folderDetails,$true)
Assert 'folders:junk-switch-only-adds-junk' ($receiving.Count -eq 2)
Assert 'folders:sent-drafts-trash-all-excluded' ($folderDetails[3].ExcludedFromReceive -and $folderDetails[4].ExcludedFromReceive -and $folderDetails[5].ExcludedFromReceive -and $folderDetails[6].ExcludedFromReceive)
Assert 'folders:outgoing-subfolder-excluded' (-not ($receiving | Where-Object {$_.Name -eq 'Sent/Archive'}))
$rules=$asm.GetType('Codepass.MailRules')
$cnSent=[string][char]0x5DF2+[char]0x53D1+[char]0x9001+[char]0x90AE+[char]0x4EF6
Assert 'folders:chinese-sent-excluded' (Call $rules 'IsOutgoingFolderName' $null @($cnSent))
$hierarchyWire="* LIST () `".`" `"INBOX.Sent.2026`"`r`n* LIST (\Trash) `"/`" `"DeletedWork`"`r`n* LIST () `"/`" `"DeletedWork/2026`"`r`n* LIST (\Sent \Noselect) `".`" `"CustomOut`"`r`n* LIST () `".`" `"CustomOut.Year`"`r`n* LIST () `".`" `"INBOX.CategoryA`"`r`n* LIST () NIL `"Category.B`"`r`nC1 OK list`r`n"
$pair=Replay $hierarchyWire
$hierarchy=Call $imapType 'ListFolderDetails' $pair[0] @()
$safeFolders=Call $watcher 'ReceiveFolders' $null @($hierarchy,$true)
Assert 'folders:server-dot-delimiter-preserved' ($hierarchy[0].Delimiter -eq '.')
Assert 'folders:dot-sent-path-excluded' (-not ($safeFolders | Where-Object {$_.Name -eq 'INBOX.Sent.2026'}))
Assert 'folders:special-trash-descendant-excluded' (-not ($safeFolders | Where-Object {$_.Name -eq 'DeletedWork/2026'}))
Assert 'folders:special-noselect-sent-descendant-excluded' (-not ($safeFolders | Where-Object {$_.Name -eq 'CustomOut.Year'}))
Assert 'folders:valid-category-and-flat-dot-kept' ($safeFolders.Count -eq 2)
$cfg.EmailEnabled=$true; $cfg.EmailCheckJunk=$false
$categoryGen=Call $watcher 'BeginJunkGeneration' $null @()
Assert 'folders:category-commit-not-blocked-by-junk-off' (Call $watcher 'CanCommit' $null @($categoryGen,$true,$cfg))
$categoryPath=Join-Path $root 'category-history.txt'
[void](Call $history 'Init' $null @($categoryPath))
$category=[Activator]::CreateInstance($asm.GetType('Codepass.ImapFolder'),$true)
$category.Name='CategoryA'; $category.Selectable=$true
$examine="* 1 EXISTS`r`n* OK [UIDVALIDITY 42] validity`r`n* OK [UIDNEXT 10] next`r`nC1 OK examined`r`n"
$categoryReplay=$examine+"* SEARCH 9`r`nC2 OK searched`r`n"+(MailWire 3 9 'category-existing@example.com' $now)+"* SEARCH 9`r`nC7 OK searched`r`n"
$pair=Replay $categoryReplay
[void](Call $watcher 'ProcessReceiveFolder' $null @($pair[0],$categoryGen,$cfg,$category,$false))
Assert 'folders:initial-category-mail-collected' ((Call $history 'Snapshot' $null @()).Count -eq 1)
Assert 'folders:initial-category-is-history-only' (-not (Call $history 'Snapshot' $null @())[0].CanBeLatest)
$categoryKey=Call $watcher 'MailboxKey' $null @($cfg,'CategoryA')
$waterArgs=[object[]]@($categoryKey,[long]42,[long]0)
[void]$configType.GetMethod('TryMailboxWater',$flags).Invoke($cfg,$waterArgs)
Assert 'folders:category-independent-water' ($waterArgs[2] -eq 9)
$newExamine="* 2 EXISTS`r`n* OK [UIDVALIDITY 42] validity`r`n* OK [UIDNEXT 11] next`r`nC1 OK examined`r`n"
$newReplay=$newExamine+"* SEARCH 10`r`nC2 OK searched`r`n"+(MailWire 3 10 'category-new@example.com' $now)+"* SEARCH 10`r`nC7 OK searched`r`n"
$pair=Replay $newReplay
[void](Call $watcher 'ProcessReceiveFolder' $null @($pair[0],$categoryGen,$cfg,$category,$false))
Assert 'folders:new-category-mail-collected' ((Call $history 'Snapshot' $null @()).Count -eq 2)
Assert 'folders:new-category-updates-latest' ((Call $history 'Snapshot' $null @())[0].CanBeLatest)
$raceCategory=[Activator]::CreateInstance($asm.GetType('Codepass.ImapFolder'),$true)
$raceCategory.Name='CategoryRace'; $raceCategory.Selectable=$true
$racePath=Join-Path $root 'category-race.txt'
[void](Call $history 'Init' $null @($racePath))
$raceWire=$examine+"* SEARCH 9 10`r`nC2 OK search`r`n"+(MailWire 3 9 'race-existing@example.com' $now)+"* SEARCH 10`r`nC7 OK search`r`n"+(MailWire 8 10 'race-new@example.com' $now)+"* SEARCH`r`nC12 OK search`r`n"
$pair=Replay $raceWire
[void](Call $watcher 'ProcessReceiveFolder' $null @($pair[0],$categoryGen,$cfg,$raceCategory,$false))
$raceItems=Call $history 'Snapshot' $null @()
Assert 'folders:first-scan-race-both-mails-recorded' ($raceItems.Count -eq 2)
Assert 'folders:first-scan-race-new-mail-normal-delivery' ($raceItems[0].CanBeLatest -and -not $raceItems[1].CanBeLatest)
Assert 'folders:first-scan-race-new-uid-fetched-once' ([regex]::Matches($pair[1].Written,'UID FETCH 10 \(UID INTERNALDATE').Count -eq 1)
$failedCategory=[Activator]::CreateInstance($asm.GetType('Codepass.ImapFolder'),$true)
$failedCategory.Name='CategoryFailure'; $failedCategory.Selectable=$true
$pair=Replay ($examine+"C2 NO temporary failure`r`n")
Assert 'folders:first-category-failure-retried' (Rejected { Call $watcher 'ProcessReceiveFolder' $null @($pair[0],$categoryGen,$cfg,$failedCategory,$false) })
$failedKey=Call $watcher 'MailboxKey' $null @($cfg,'CategoryFailure')
$failedArgs=[object[]]@($failedKey,[long]42,[long]0)
$failedKnown=$configType.GetMethod('TryMailboxWater',$flags).Invoke($cfg,$failedArgs)
Assert 'folders:failed-first-check-no-water-commit' (-not $failedKnown)
$junkEvent=$watcher.GetField('junkWake',$flags).GetValue($null); $junkEvent.Reset()
[void](Call $watcher 'Kick' $null @())
Assert 'folders:qq-reminder-wakes-category-check' $junkEvent.IsSet

$manyInbox=[Activator]::CreateInstance($asm.GetType('Codepass.Mailbox'),$true)
$manyInbox.Name='INBOX'; $manyInbox.UidValidity=7; $manyInbox.UidNext=31
$gen=Call $watcher 'BeginMainGeneration' $null @()
$inboxBefore=Call $watcher 'InitializeWater' $null @($cfg,$manyInbox,$gen,$false)
$manyFolders=New-Object 'System.Collections.Generic.List[object]'
for($number=0;$number -lt 65;$number++) {
    $manyFolder=[Activator]::CreateInstance($asm.GetType('Codepass.ImapFolder'),$true)
    $manyFolder.Name='ManyCategory'+$number; $manyFolder.Selectable=$true; $manyFolders.Add($manyFolder)
    $pair=Replay ($examine+"* SEARCH`r`nC2 OK recent`r`n* SEARCH 9`r`nC3 OK new`r`n")
    [void](Call $watcher 'ProcessReceiveFolder' $null @($pair[0],$categoryGen,$cfg,$manyFolder,$false))
}
$allFoldersKept=$true; $allSecondScansNormal=$true
foreach($manyFolder in $manyFolders) {
    $manyKey=Call $watcher 'MailboxKey' $null @($cfg,$manyFolder.Name)
    $manyArgs=[object[]]@($manyKey,[long]42,[long]0)
    if(-not $configType.GetMethod('TryMailboxWater',$flags).Invoke($cfg,$manyArgs) -or $manyArgs[2] -ne 9) { $allFoldersKept=$false }
    $pair=Replay ($examine+"* SEARCH 9`r`nC2 OK new`r`n")
    [void](Call $watcher 'ProcessReceiveFolder' $null @($pair[0],$categoryGen,$cfg,$manyFolder,$false))
    if($pair[1].Written.Contains('SEARCH SINCE')) { $allSecondScansNormal=$false }
}
Assert 'folders:65-category-watermarks-preserved' $allFoldersKept
Assert 'folders:65-category-second-round-no-reinitialization' $allSecondScansNormal
$manyInbox.UidNext=40
$inboxAfter=Call $watcher 'InitializeWater' $null @($cfg,$manyInbox,$gen,$false)
Assert 'folders:inbox-reconnect-water-not-evicted' ($inboxAfter -eq $inboxBefore)
$manyLoaded=Call $configType 'Load' $null @([string](Join-Path $root 'config.ini'))
$inboxArgs=[object[]]@((Call $watcher 'MailboxKey' $null @($cfg,'INBOX')),[long]7,[long]0)
$persistedInbox=$configType.GetMethod('TryMailboxWater',$flags).Invoke($manyLoaded,$inboxArgs)
Assert 'folders:inbox-and-65-state-persisted' ($persistedInbox -and $inboxArgs[2] -eq $inboxBefore -and ($manyLoaded.EmailMailboxState.Split("`n")).Length -ge 66)
[void](Call $history 'Init' $null @($historyPath))

$cfg.EmailEnabled=$false
$uiPath=[string](Join-Path $root 'ui.ini')
$window=[Activator]::CreateInstance($windowType,$flags,$null,[object[]]@($cfg.PSObject.BaseObject,$uiPath),$null)
$row=Call $windowType 'BuildRow' $window @($items[0])
$rowHost=New-Object Windows.Controls.Border
$rowHost.Background=[Windows.Media.Brushes]::White
$rowHost.Padding=New-Object Windows.Thickness(10)
$rowHost.Child=$row
SaveVisual $rowHost 920 64 'history-sender-centered.png'
$senderCell=$row.Child.Children[1]; $rawCell=$row.Child.Children[3]
Assert 'ui:tooltip-content-lazy' ($rawCell.ToolTip -is [string])
[void](Call $windowType 'EnsureRawTooltip' $null @($rawCell,$items[0]))
Assert 'ui:copy-is-keyboard-button' ($row.Child.Children[2] -is [Windows.Controls.Button] -and $row.Child.Children[2].Focusable)
Assert 'ui:raw-action-is-button' ($row.Child.Children[4] -is [Windows.Controls.Button] -and $row.Child.Children[4].Focusable)
Assert 'ui:sender-centered' ($senderCell.TextAlignment.ToString() -eq 'Center')
Assert 'ui:raw-remains-left' ($rawCell.TextAlignment.ToString() -eq 'Left')
Assert 'ui:tooltip-wraps-full-original' ($rawCell.ToolTip.Content.Content.Text -eq $items[0].Raw -and $rawCell.ToolTip.Content.Content.TextWrapping.ToString() -eq 'Wrap')
Assert 'ui:tooltip-scrollable' ($rawCell.ToolTip.Content.VerticalScrollBarVisibility.ToString() -eq 'Auto')
Assert 'ui:tooltip-show-delay' ([Windows.Controls.ToolTipService]::GetInitialShowDelay($rawCell) -eq 300)
$window.Show()
$dialog=Call $windowType 'CreateHistoryRawWindow' $window @($items[0])
$original=$dialog.Content.Children[1]
Assert 'ui:full-original-not-truncated' ($original.Text -eq $items[0].Raw)
Assert 'ui:full-original-selectable-readonly' ($original.IsReadOnly -and $original.AcceptsReturn)
Assert 'ui:full-original-scrollable-resizable' ($original.VerticalScrollBarVisibility.ToString() -eq 'Auto' -and $dialog.ResizeMode.ToString() -eq 'CanResize')
Assert 'ui:dialog-owned-by-main-window' ($dialog.Owner -eq $window)
SaveVisual $dialog.Content 720 460 'history-full-original.png'
$dialog.Close()
Assert 'ui:doubleclick-opens-full-original' ([HistoryClickProbe]::DoubleClickAndClose($window,$rawCell,$items[0].Raw))
$overlay=$window.Content.FindName('lockOverlay')
$overlay.Visibility=[Windows.Visibility]::Visible
Assert 'ui:locked-window-blocks-raw-dialog' (-not [HistoryClickProbe]::DoubleClickAndClose($window,$rawCell,$items[0].Raw))
$overlay.Visibility=[Windows.Visibility]::Collapsed
$cfg.LockEnabled=$true; $cfg.AutoLockEnabled=$true
$lockedDialog=Call $windowType 'CreateHistoryRawWindow' $window @($items[0])
$lockedDialog.Show()
[void](Call $windowType 'LockWindow' $window @())
Assert 'ui:locking-closes-existing-raw-window' (-not $lockedDialog.IsVisible -and $overlay.Visibility.ToString() -eq 'Visible')
$overlay.Visibility=[Windows.Visibility]::Collapsed
$minimizedDialog=Call $windowType 'CreateHistoryRawWindow' $window @($items[0])
$minimizedDialog.Show()
$minimizedDialog.WindowState=[Windows.WindowState]::Minimized
Assert 'ui:minimizing-raw-window-locks' ($overlay.Visibility.ToString() -eq 'Visible' -and -not $minimizedDialog.IsVisible)
$cfg.LockEnabled=$false; $cfg.AutoLockEnabled=$false; $overlay.Visibility=[Windows.Visibility]::Collapsed
$empty=[Activator]::CreateInstance($itemType,$true); $empty.Channel='email'; $empty.Raw=''
$emptyText=Call $windowType 'HistoryRawText' $null @($empty)
Assert 'ui:unsaved-email-explained' ($emptyText.Length -gt 10)
Assert 'ui:email-status-card-removed' ($null -eq $window.Content.FindName('btnEmailRecheck') -and $null -eq $window.Content.FindName('lblEmailStatus') -and $null -eq $window.Content.FindName('lblEmailProcessing') -and $null -eq $window.Content.FindName('cardEmailStatus'))
Assert 'ui:email-test-result-label-present' ($null -ne $window.Content.FindName('lblEmailTestResult'))
$cfg.EmailEnabled=$true
[void](Call $windowType 'UpdateEmailStatus' $window @())
Assert 'ui:qq-status-follows-configuration' ($window.Content.FindName('lblQqStatus').Text.Length -gt 0)
$cfg.EmailEnabled=$false
# Render only synthetic rows, disabling startup opacity animation.
$window.Content.Opacity=1
$window.Content.Measure((New-Object Windows.Size(960,560)))
$window.Content.Arrange((New-Object Windows.Rect(0,0,960,560)))
$window.Content.UpdateLayout()
[void](Call $windowType 'ExitApp' $window @())
Write-Output ('FIXTURES='+$root)
Write-Output ('TOTAL pass='+$pass+' fail='+$fail)
if($fail -gt 0) { exit 1 }
