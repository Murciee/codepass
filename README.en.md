<div align="center">

<img src="assets/codepass-sms.png" width="112" alt="codepass" />

# codepass

**SMS and email verification codes, copied automatically to Windows.**

Android SMS or email code → Windows → clipboard

[![Release](https://img.shields.io/github/v/release/Murciee/codepass?label=release&color=2f75f0)](https://github.com/Murciee/codepass/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%20%2F%2011-2f75f0)](#quick-setup)
[![Android](https://img.shields.io/badge/Android-6%2B-2f75f0)](#phone-options)
[![License](https://img.shields.io/badge/License-MIT-2f75f0)](LICENSE)

[Features](#features) · [Screenshots](#feature-screenshots) · [Downloads](#downloads) · [Quick Setup](#quick-setup) · [Settings](#settings) · [FAQ](#faq)

[简体中文](README.md) · **English**

</div>

> [!WARNING]
> A personal open-source project with no support or compatibility guarantees. The Windows package is not code-signed. Read the [Disclaimer](DISCLAIMER.md) and [Privacy Notice](PRIVACY.md) before use.

## Features

| Capability | Description |
| --- | --- |
| 📱 SMS codes | Forward from Android over LAN, EasyTier or ntfy |
| 📧 Email codes | QQ, 163, 126, Gmail, Outlook and custom IMAP; preserves read/unread status |
| 📋 Auto copy | Paste codes as they arrive, with optional notifications and clipboard clearing |
| 🕘 History | Search, SMS/email filters and original text; 10-minute cleanup by default, `0` disables |
| 🔐 Privacy | Separate SMS/email notification modes, passcode lock and redacted diagnostics |
| 🖥️ Tray | Silent startup with Windows; double-click to open, resizable window |

## Feature Screenshots

<table>
  <tr>
    <td width="50%" align="center"><b>Code history</b><br><img src="assets/v1.0.4/history-search.png" width="100%" alt="History search, SMS and email filters, automatic cleanup"></td>
    <td width="50%" align="center"><b>Email settings</b><br><img src="assets/v1.0.4/email-status-narrow.png" width="100%" alt="Email connection and reception status"></td>
  </tr>
  <tr>
    <td width="50%" align="center"><b>SMS settings</b><br><img src="assets/v1.0.4/sms-settings-narrow.png" width="100%" alt="SMS connection and silent startup"></td>
    <td width="50%" align="center"><b>Privacy settings</b><br><img src="assets/v1.0.4/privacy-settings.png" width="100%" alt="SMS and email notification privacy, passcode lock"></td>
  </tr>
</table>

## Downloads

Current Windows version: **v1.0.5**. Phone modules remain at v1.0.3. [Download the latest release](https://github.com/Murciee/codepass/releases/latest).

| Asset | Purpose |
| --- | --- |
| `codepass-windows.zip` | Extract and run `codepass.exe` |
| `codepass-sms-magisk.zip` | Magisk phone module |
| `codepass-lsposed-release.apk` | LSPosed phone module |
| `SHA256SUMS.txt` | Asset checksums |

Upgrade Windows by quitting from the tray and replacing `codepass.exe`. Set history cleanup to `0` for long-term retention.

## Phone Options

| Phone | Guide |
| --- | --- |
| Not rooted | [No-root Webhook](phone/no-root.md) |
| Magisk | [Magisk module](phone/magisk/README.md) |
| Rooted | [Root script](phone/README.md) |
| LSPosed | [LSPosed module](phone/lsposed/README.md) |

Use one SMS forwarding option. Email reception is configured directly on Windows and needs no phone app.

## Quick Setup

### SMS

1. Open SMS settings, enter a random access token and the PC IPv4 address, then restart.
2. Webhook URL: `http://PC-IPv4:8787/sms`; send the token in the `X-Token` header.
3. Click Test connectivity and send a test SMS.

### Email

Open Email settings, select a provider, enter the account and IMAP authorization code/app password, test the connection and enable reception. Gmail setup is in [Gmail app passwords](docs/GMAIL_APP_PASSWORD.md).

Closing the window hides it to the tray; quit from the tray menu.

## Settings

| Page | Main options |
| --- | --- |
| History | Search and filters, click-to-copy, original text, automatic cleanup |
| SMS settings | Address/port/token, silent startup, code length, filters, ntfy, clipboard clearing |
| Email settings | Connection status, subject/sender rules, recheck, junk folders, body retention, QQ integration |
| Privacy settings | SMS/email notification content, passcode lock, auto-lock |
| About | Update check, preview and export redacted diagnostics |

Settings save automatically. Restart after changing network listening or ntfy; email settings apply immediately. History cleanup never deletes mailbox messages.

## Development

See the [Windows code map](https://github.com/Murciee/codepass/blob/main/docs/ARCHITECTURE.md) for the code structure, build boundaries and maintenance entry points.

## FAQ

- **Can't reach the PC**: check the IPv4 address, port, token and Windows Firewall.
- **Can't copy**: check whether another clipboard tool is holding the clipboard.
- **Missing email codes**: check connection status and filter rules, then recheck the last 30 minutes if needed.
- **Can't find a code**: select All in History and search the service, sender or code.
- **Need troubleshooting help**: use About → Preview redacted diagnostics; do not share a full config or log.

## Security and Privacy

- Use a random LAN token and never expose the port to the internet.
- Use HTTPS for ntfy; a public topic is not reliable access control.
- Protect configuration, logs and notifications; never publish real codes, messages or tokens.
- The passcode lock protects the UI; notification masking does not change clipboard content.

## User Guides

- [No-root Webhook](phone/no-root.md)
- [Magisk](phone/magisk/README.md)
- [LSPosed](phone/lsposed/README.md)
- [Root / script](phone/README.md)
- [ntfy](phone/ntfy.md)

## License

Released under the [MIT License](LICENSE). Also read the [Disclaimer](DISCLAIMER.md), [Privacy Notice](PRIVACY.md), [Security](SECURITY.md) and [Support](SUPPORT.md).

## Star History

[![Star History Chart](https://api.star-history.com/svg?repos=Murciee/codepass&type=Date)](https://star-history.com/#Murciee/codepass&Date)
