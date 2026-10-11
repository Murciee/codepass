# codepass LSPosed 模块

将验证码短信转发到 Windows，支持双电脑和 ntfy。

这是实验性方案。Android 版本、厂商 ROM 和电话进程实现可能不同，不能保证所有设备兼容。没有 LSPosed 使用经验时，建议优先使用 [免 root Webhook](../no-root.md) 或 [Magisk 模块](../magisk/README.md)。

## 安装和启用

1. 从 [下载页](https://github.com/Murciee/codepass/releases/latest) 安装 `codepass-lsposed-release.apk`；
2. 打开 `codepass` 应用，填写电脑 IPv4、端口和令牌；
3. 点击“保存配置”（无需 root）；
4. 在 LSPosed 模块列表中启用 codepass；
5. 作用域只需选择 `com.android.phone`；
6. 重启手机或重启电话进程。

保存后即时生效，清空全部目标并保存可停止转发。

## 配置项目

```sh
PC_IP="192.168.1.100"
PC_PORT="8787"
TOKEN="与 Windows 端相同的令牌"
PC2_IP=""
PC2_PORT="8787"
PC2_TOKEN=""
NTFY_SERVER="https://ntfy.sh"
NTFY_TOPIC=""
NTFY_TOKEN=""
MIN_LEN="4"
MAX_LEN="8"
```

局域网填写电脑地址、端口和相同令牌；第二台电脑使用 `PC2_*`。ntfy 使用 HTTPS，主题留空停用。

## 日志和排查

未收到短信时检查：

- LSPosed 是否启用模块；
- 作用域是否包含 `com.android.phone`；
- 是否重启电话进程；
- 电脑地址、端口、令牌和防火墙。

LSPosed 日志页可搜索 `codepass-lsposed`。ROM 不兼容时可改用 Magisk 或免 root Webhook。

Windows 支持短信与邮箱验证码，界面见 [功能截图](../../README.md#功能截图)。排障使用“关于 → 预览脱敏诊断”，不要公开真实正文、验证码或完整配置。
