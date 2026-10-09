<div align="center">

<img src="assets/codepass-sms.png" width="112" alt="codepass" />

# codepass

**Sync Android SMS verification codes to the Windows clipboard.**

Android phone forwards the SMS → Windows detects the code → writes it to the clipboard.

[![Release](https://img.shields.io/github/v/release/Murciee/codepass?label=release&color=2f75f0)](https://github.com/Murciee/codepass/releases/latest)
[![Downloads](https://img.shields.io/github/downloads/Murciee/codepass/total?label=downloads&color=2f75f0)](https://github.com/Murciee/codepass/releases)
[![Platform](https://img.shields.io/badge/Windows-10%20%2F%2011-2f75f0)](#quick-start-lan-mode)
[![Android](https://img.shields.io/badge/Android-6%2B-2f75f0)](#recommended-setup)
[![License](https://img.shields.io/badge/License-MIT-2f75f0)](LICENSE)
[![Stars](https://img.shields.io/github/stars/Murciee/codepass?style=social)](https://github.com/Murciee/codepass/stargazers)

[Quick Start](#quick-start-lan-mode) · [Recommended Setup](#recommended-setup) · [Public Network](#public-network-ntfy) · [Building](#building) · [FAQ](#faq) · [Documentation](#documentation-index)

[简体中文](README.md) · **English**

</div>

> [!WARNING]
> **Release status: free beta.** This is a personal open-source tool shared for free, not a commercial product. It comes with **no support and no promise of compatibility or ongoing maintenance**. Automated testing does not replace real-device compatibility checks; the Windows package is not code-signed, and unsigned APKs cannot be installed directly.
>
> **Read the [Disclaimer](DISCLAIMER.md) and [Privacy Notice](PRIVACY.md) before use.** This tool handles SMS verification codes and carries risks of leakage, misdetection, and failed forwarding. For anything important, always refer to the original SMS on your phone.

```text
New Android SMS → LAN / EasyTier / ntfy → Windows detects the code → clipboard
```

## Features

| Capability | Description |
| --- | --- |
| 📋 Auto copy | Writes the detected code to the Windows clipboard, with optional desktop notification and auto-clear |
| 🌐 Two channels | LAN HTTP (default `8787`); optional ntfy relay over the internet; or an EasyTier network |
| 🖥️ Tray resident | Native single-file `codepass.exe`, depending only on the built-in .NET Framework 4.x; closing the window hides it to the tray without interrupting reception |
| 🔐 Privacy & security | Access token, three levels of notification masking, program passcode lock, config import/export |
| 🕘 Recent history | Keeps the last 100 records; click a code to copy it (with a light toast) |
| 📱 Multiple phone options | No-root Webhook, root script, Magisk module, LSPosed module — pick one |
| 🎨 Interface | Native Fluent rounded UI on Windows; lightweight WebUI for Magisk; the Flutter settings page for LSPosed is archived and kept only for local historical review |

## Recommended Setup

Most users should start with “LAN + no-root Webhook”: simple to configure, low latency, and no bootloader unlock or root required.

| Phone environment | Recommended option | Notes |
| --- | --- | --- |
| 🌱 Not rooted | [No-root guide](phone/no-root.md) | Use a Webhook app such as SmsForwarder |
| 🧱 Magisk installed | [Magisk guide](phone/magisk/README.md) | Runs at boot; open the WebUI from a supported manager |
| ⌨️ Rooted, prefers scripts | [Root client](phone/README.md) | Easy to configure and debug by hand |
| 🔌 LSPosed installed | [LSPosed guide](phone/lsposed/README.md) | Experimental; compatibility depends on your ROM |

Enable only one forwarding option to avoid duplicate messages. The root/Magisk options also require the ROM to allow reading the SMS database.

## Quick Start: LAN Mode

### 🖥️ 1. Prepare the Windows side

You need a Windows 10/11 x64 PC and an Android phone. Just run `codepass.exe` — a single file, no installation. Config, history, and logs are stored next to the EXE, so that folder must be writable by the current user. On first launch, if Windows Firewall asks for network access, allow “Private networks”.

On the “Settings” page, configure the following:

1. Click “Detect local address” and note your PC's IPv4 address;
2. Enter a random string in “Access token” (leaving it empty disables authentication — not recommended; see [Security](#security-recommendations));
3. Changes are saved automatically. **The access token takes effect immediately; changing the port or ntfy settings requires quitting from the tray and restarting the program**;
4. Click “Test connectivity” to confirm the service is healthy;
5. Enter the PC address, port, and token on the phone; root-script users can click “Copy phone config” to copy the parameter block to the clipboard.

The access token must match the one on the PC.

### 📱 2. Configure the phone side

Using a Webhook app as an example, the target URL is:

```text
http://PC-IPv4:8787/sms
```

The token can be passed via a request header or a URL parameter:

```text
X-Token: your-token
```

```text
http://PC-IPv4:8787/sms?token=your-token
```

Use `POST`, and set the body to the SMS body variable (the exact syntax depends on your app). See the [no-root guide](phone/no-root.md) for details. After saving the rule, use the app's test function to verify connectivity.

### ✅ 3. Verify the result

The tray menu's “Copy latest code” copies the most recent code. When a real SMS arrives, the detected code is written to the clipboard and shown on the history page; notification-type messages that are not codes are filtered out, so runs of digits in their body do not create records.

Window behavior: closing the window only hides it to the tray, and background reception keeps running; open it again from the tray. To fully exit, right-click the tray icon and choose “Exit”. Minimizing only minimizes the window. If “Auto lock” is enabled, reopening the window, restoring from minimized, or reopening after closing will require unlocking; when “Auto lock” is disabled, only clicking “Lock now” locks it.

> The PC-side “Test connectivity” only verifies HTTP access from the PC to that address; it does not replace a phone-to-PC network test. Finally, send a test SMS from your phone to verify the full path.

## Windows Settings

| Setting | Description |
| --- | --- |
| Listen port | TCP port the phone connects to, default `8787` |
| Access token | Shared token for the LAN interface, masked by default; use the “Show / Hide” button to toggle |
| Start with Windows | Launches automatically after the current user logs in, no admin rights required |
| PC IPv4 address | The address the phone uses; click “Detect local address” to fill it in |
| Code length | Digit length of auto-detected codes, default 4–8, adjustable to 1–32 |
| Auto-clear clipboard | Seconds after which the clipboard is cleared; `0` disables it |
| Desktop notification | Whether to show a tray notification when a code arrives |
| Privacy | Notification content: only “received” (default), show content with the code replaced by asterisks, or show the full SMS text |
| Message filter | Multi-line keywords or .NET regular expressions; any match makes the SMS ignored — not recorded and not copied |
| Program passcode lock | Enable a passcode for the settings and history pages, with optional auto-lock and a “Lock now” action |
| About & update check | The “About” section shows the version and GitHub page and checks for updates; a new version is downloaded and applied on restart |
| ntfy settings | Relay server, topic, and access token for public-network use |

Additional notes:

- **Saving behavior**: fields save automatically on blur or Enter, when toggles change, when a dropdown is selected, and after secret input completes. The access token, code length, and notification content take effect immediately; the port and ntfy subscription apply on next launch.
- **Listen address**: the service listens on `0.0.0.0:<port>` (all adapters). The “PC IPv4 address” in Settings only generates and displays the phone config; it does not change the listening adapter.
- **Auto-clear**: after the set number of seconds the clipboard is emptied, without checking whether another program has rewritten its content.
- **Notification masking**: by default it only says “a new code arrived”. “Show content, mask the code” keeps the full text but replaces the detected code with asterisks (*). “Show full SMS content” keeps the code in the notification and may leak it on a lock screen or to onlookers; content reflects what the receiver actually got, and long text is subject to system notification limits.
- **Program passcode lock**: protects interface access only; it does not encrypt local config, history, or logs. The password uses PBKDF2-HMAC-SHA1 (100000 rounds, 16-byte salt, 32-byte verifier). Auto-lock is off by default. If the lock data is corrupted, the lock does not auto-release and needs the original full config to recover.
- **History page**: the window opens on the “History” page by default, showing the last 100 records as “time / sender / code / original text”. The sender is taken from the first `【XXXX】` field in the SMS body, otherwise shown as “Unknown sender”. Clicking a code copies it with a light toast (clicking elsewhere does not copy).
- **Left navigation**: settings are grouped into “General / Security / About”. General merges PC reception, phone connection, ntfy, and config management, with config management at the bottom of the page. The main window can be resized by dragging its edges and remembers its size next launch. Security holds passcode management and “Lock now”.
- **About**: the “About” page keeps only the version, GitHub page, and “Check for updates”; a new version is downloaded automatically and applied on restart, with no manual steps.
- **SMS filtering**: built-in filters cover long messages with obvious notification semantics such as telecom-fraud warnings, overseas-service opt-outs, SMS-to-subscribe messages, and hotlines. You can also enter custom keywords or .NET regular expressions line by line in General; any match makes the SMS ignored. An invalid regex never blocks SMS reception.
- **Data files**: `config.ini` (config), `history.txt` (recent records), and `app.log` (runtime log) live next to the EXE; the log is truncated and rewritten once it exceeds about 2 MiB.

When exporting config you can choose whether to include the LAN token, ntfy token, and passcode lock info; importing a redacted config keeps the current tokens, ntfy topic, and passcode lock, and preserves the current security settings.

## Public Network: ntfy

When the phone and PC are not on the same network, you can use ntfy as a public relay:

1. Use the public service `https://ntfy.sh`, or deploy your own HTTPS ntfy service;
2. Create a topic name long enough to be unguessable;
3. Enter the same server, topic, and access token on Windows and on the phone;
4. Enter the config on Windows (changes save automatically), then quit from the tray and restart; configure the phone module per its documentation.

Windows subscribes to ntfy's streaming API and extracts the body from `message` events; it de-duplicates by message ID (keeping the last 200), reconnects every 5 seconds after a disconnect, and does not replay already-processed messages on reconnect.

A public topic name is not a full access-control mechanism, so do not use a guessable name. The ntfy server must use HTTPS; for handling verification codes prefer a private HTTPS service with authentication and access control. See [`phone/ntfy.md`](phone/ntfy.md) for self-hosting.

## Network Interface

LAN receive endpoint:

```http
POST http://PC-IPv4:8787/sms
Content-Type: text/plain
```

The body can be a verification code, a full SMS, or JSON:

```text
654321
```

```json
{"code":"654321"}
```

Extraction tries, in order: the whole body as a code, the JSON `code` field, keyword context (验证码/校验码/动态密码/OTP and similar), and a standalone 4–8 digit number (6 digits preferred). A `{"ok":true}` response means the request was accepted; check the history page or logs to see whether a code was detected.

Health check endpoint:

```http
GET http://PC-IPv4:8787/health
X-Token: same token as the PC
```

Returns `{"ok":true}` on success, without writing a record or touching the clipboard.

The token is passed via the `X-Token` header or the `?token=` URL parameter; **when an access token is set, both `/sms` and `/health` validate it**, and when the token is empty no check is performed. A mismatched token returns HTTP `403`. The request body limit is about 1 MiB and the concurrent connection limit is 32. Local HTTP is unencrypted — do not expose the listen port directly to the internet.

## FAQ

### 📶 The phone cannot connect to the PC

Check in order:

1. Are the phone and PC on the same Wi-Fi or the same EasyTier network?
2. Is the address entered on the phone the PC's current IPv4 address?
3. Do the port and token match the Windows side?
4. Does Windows Firewall allow inbound connections on the private network or EasyTier adapter?
5. Does the PC-side “Test connectivity” succeed?

With EasyTier, enter the IPv4 address of the Windows EasyTier virtual adapter, not the ordinary Wi-Fi address.

### 🔍 An SMS arrives but no code is detected

Confirm the SMS body reached the PC, and check the “Code length” setting. Some messages use mixed letters and digits, or a code length outside the current range.

### 📋 No notification and nothing on the clipboard after an SMS

A successful write shows “Code copied”; if it still fails after about 10 seconds (with desktop notifications on), it shows “Code received, copy failed”. If `app.log` contains `clipboard error` (for example `OpenClipboard failed, win32=5`; `CLIPBRD_E_CANT_OPEN` in older versions), the PC's clipboard is being held or locked by another program:

1. Restarting the PC usually fixes it quickly;
2. Common culprits: game accelerators (e.g. NetEase UU), clipboard managers/enhancers (Ditto, PowerToys, Snipaste, etc.), remote-control tools (ToDesk, Sunlogin, the Remote Desktop `rdpclip`), and some input methods and office suites;
3. If it recurs, exit these programs one by one to identify the cause. If the copy-failure notice is frequent, confirm codepass runs in the **current logged-in user session** (do not start it from a scheduled task set to run whether or not a user is logged on).

The program now writes the clipboard via Win32 directly (without deferred rendering); brief locks are retried for about 10 seconds and the result is verified. Only a persistent lock that ultimately fails shows “copy failed”, and the code is still kept on the history page for manual copying.

### 🔄 I changed the port or ntfy but it didn't take effect

The port and ntfy subscription apply only at startup. Right-click the tray icon, choose “Exit”, and run the program again. The access token takes effect immediately without a restart.

### 🔒 Forwarding stops after the screen locks

The no-root app needs battery optimization disabled, autostart and background running allowed, and the task locked in the recents list. Setting names vary by brand.

### 🧩 Magisk has no WebUI

Magisk WebUI requires the manager to support it; when it doesn't, you can still edit `config.conf` manually. See the [Magisk guide](phone/magisk/README.md).

## Security Recommendations

- Always set a random access token for the LAN interface; an empty token disables authentication;
- Never publish the ntfy topic or access token to a public repository;
- The ntfy server must use HTTPS;
- `app.log`, `history.txt`, and phone config files may contain codes or tokens — protect them accordingly;
- The LSPosed config must be readable by `com.android.phone`; its permission model differs from Magisk — see its dedicated notes.

## Risk, Disclaimer and License

This project is a **free, beta-stage personal open-source tool** provided “as is”, with no express or implied warranty and no promise of support or ongoing maintenance. To the maximum extent permitted by applicable law, the author is not liable for losses arising from use of this tool; **nothing here excludes liability that cannot be excluded or limited under applicable law**. By starting to use it you confirm that you understand and accept this.

Read before use:

- [`DISCLAIMER.md`](DISCLAIMER.md): full disclaimer and risk list;
- [`PRIVACY.md`](PRIVACY.md): what data is handled, where it is stored, how to clear it;
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md): third-party components and licenses;
- [`SECURITY.md`](SECURITY.md): how to report security issues;
- [`SUPPORT.md`](SUPPORT.md): support boundaries;
- [`CHANGELOG.md`](CHANGELOG.md): change log.

The source is released under the [MIT License](LICENSE). **Note that MIT also permits others to use, modify, and redistribute it commercially**; third-party components remain under their own licenses.

## Known Limitations and Release Requirements

- **Secure defaults**: the Windows client does **not enforce** an access token; with an empty token the LAN interface is unauthenticated. Always set a random token and restrict firewall scope; never map it directly to the internet.
- **Privacy boundary**: LAN HTTP is unencrypted; the program passcode does not encrypt config or history. Auto-clear only empties the current clipboard content and does not affect clipboard history, cloud sync, or copies held by other apps; notification masking is not full SMS privacy protection.
- **Reliability**: there is currently no persistent send queue, end-to-end delivery guarantee, or offline message replay. Sleep, disconnects, process exits, and ROM restrictions can cause loss; ntfy reconnect only de-duplicates by message ID and does not guarantee replay; running several forwarding options at once can cause duplicates.
- **Detection scope**: heuristic detection of numeric codes only; not every SMS format is guaranteed. Verify the source of codes for anything important; a detected code is not proof of the SMS's authenticity.
- **Release signing**: Android release builds require a fixed release certificate, and upgrades must reuse it; Windows release packages should be code-signed and integrity-checked. Debug and unsigned packages are for development verification only.
- **Compatibility testing**: a formal release should cover Windows 10/11, various DPI/monitor setups, mainstream Android ROMs, screen-lock/power-save/reboot, and network interruptions. Unverified environments are not promised to work.
- **Open source and governance**: released under the [MIT License](LICENSE), which permits use, modification, commercial use, and redistribution, provided users accept the risk and retain the copyright and license notice; third-party dependencies and runtimes are listed in [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md). See [`PRIVACY.md`](PRIVACY.md), [`SECURITY.md`](SECURITY.md), [`SUPPORT.md`](SUPPORT.md), and [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md) for privacy, security reporting, support boundaries, and release checks.

## Building

Run the commands below from the repository root. The SDK, compiler, and signing tools are needed only in a development/release environment; ordinary Windows users need no development setup.

| Target | Default artifact | Requirements |
| --- | --- | --- |
| Windows (native single file) | `windows/dist/codepass.exe` | The .NET Framework 4.x compiler bundled with Windows (`csc.exe`), no extra SDK |
| Android / LSPosed | Native debug APK (`release/codepass-lsposed-debug.apk`) | Java 17, Android SDK 36 / Build Tools 36 / NDK 28.2, Xposed API 82; the Flutter settings page build needs Flutter 3.47.6 (source archived) |
| Magisk | `release/codepass-sms-magisk.zip` | Windows PowerShell; ZIP packaging needs no Android SDK |

Windows (the currently maintained native single-file client):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File windows\build.ps1 -Legacy
```

> `-Legacy` builds the native single-file client described in this README, producing `windows/dist/codepass.exe` and automatically packaging `release/codepass-windows.zip`; it is the continuously maintained and released Windows client. Without arguments, `windows\build.ps1` delegates to the **archived** Flutter client (source under local `archive/flutter-client`, not shipped publicly) for historical build review only; both listen on `8787` by default, so do not run them at the same time.

LSPosed native fallback (the installer kept in the repository):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\lsposed\build.ps1 -Legacy
```

Magisk module:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\magisk\build.ps1
```

`phone\lsposed\build.ps1 -Legacy` produces a native-fallback debug APK and copies it to `release/codepass-lsposed-debug.apk` for installation on controlled devices; it is not a general-user release package. The native fallback also supports `-Release -SignAndroid` to directly produce a signed release APK — see the [LSPosed guide](phone/lsposed/README.md). The default (without `-Legacy`) Flutter settings-page build is archived (source under local `archive/flutter-client`); when needed, add `-Release` to produce a release-candidate APK, then, after configuring a fixed release certificate, sign it with `-SignAndroid -AndroidKeystore <file> -AndroidKeyAlias <alias>`, passing the passwords via the `CODEPASS_ANDROID_STORE_PASSWORD` and `CODEPASS_ANDROID_KEY_PASSWORD` environment variables. Debug builds may contain source locations and development-environment info and must not be published or used to assess release performance.

The build environment is provided through environment variables such as `JAVA_HOME`, `ANDROID_HOME`, and `FLUTTER_ROOT`, or the `-FlutterRoot`, `-EnvironmentRoot`, and `-Proxy` arguments; personal environment config is not stored in source or docs. See [`docs/BUILD_ANDROID.md`](docs/BUILD_ANDROID.md) for installation and verification.

## Repository Layout

```text
codepass/
├─ windows/     Native single-file Windows client (maintained, main release line)
│  ├─ src/      C# source
│  ├─ dist/     Build output codepass.exe
│  └─ build.ps1 -Legacy is the maintained build entry
├─ phone/       Phone options (Webhook / root / Magisk / LSPosed)
├─ release/     Release artifacts (Windows ZIP, Magisk ZIP, LSPosed APK)
├─ docs/        Release, build, and compliance docs
├─ tools/       Pre-release check scripts
├─ assets/      Images for docs and UI
└─ .github/     GitHub config such as Issue templates
```

Historical Flutter / Rust implementations, old build artifacts, and local backups are collected under the local `archive/` directory on the development machine and excluded by `.gitignore`; they are not shipped in the public repository.

## Documentation Index

About the project and releases:

- [`DISCLAIMER.md`](DISCLAIMER.md): disclaimer and risk list;
- [`PRIVACY.md`](PRIVACY.md): privacy notice;
- [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md): third-party components and licenses;
- [`SECURITY.md`](SECURITY.md): security reporting;
- [`SUPPORT.md`](SUPPORT.md): support and maintenance boundaries;
- [`CHANGELOG.md`](CHANGELOG.md): change log;
- [`docs/RELEASE_CHECKLIST.md`](docs/RELEASE_CHECKLIST.md): pre-release checklist;
- [`docs/RELEASE_NOTES.md`](docs/RELEASE_NOTES.md): release notes template.

Usage and building:

- [`phone/no-root.md`](phone/no-root.md): no-root Webhook setup;
- [`phone/README.md`](phone/README.md): root, script, and Magisk deployment;
- [`phone/magisk/README.md`](phone/magisk/README.md): Magisk module install and config;
- [`phone/lsposed/README.md`](phone/lsposed/README.md): LSPosed build and enabling;
- [`phone/ntfy.md`](phone/ntfy.md): self-hosted ntfy;
- [`docs/BUILD_ANDROID.md`](docs/BUILD_ANDROID.md): installing and verifying the Android / Java / Flutter build environment.

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=Murciee/codepass&type=Date)](https://star-history.com/#Murciee/codepass&Date)

<div align="center">

**[⬆ Back to top](#codepass)**

</div>
