# 手机端部署指南

手机端将短信转发给 Windows；邮箱验证码直接在电脑配置，无需手机端。短信转发只启用一种方案。

## 方案选择

| 方案 | 适用对象 | 特点 |
| --- | --- | --- |
| [免 root Webhook](no-root.md) | 普通 Android 用户 | 配置简单，推荐优先使用 |
| `forward.sh` | 已获取 root 的用户 | 便于脚本化和调试 |
| [Magisk 模块](magisk/README.md) | 使用 Magisk 的用户 | 开机自动运行，可选 WebUI |
| [LSPosed 模块](lsposed/README.md) | 已安装 LSPosed 的用户 | 实验性 hook 方案 |

## 共用配置

先在 Windows 端设置随机访问令牌和监听端口，重启程序后使用局域网接收。令牌为空只监听本机，不能接收手机请求。手机和电脑应位于同一 Wi-Fi；使用 EasyTier 时填写 Windows EasyTier 虚拟网卡的 IPv4。手机端令牌必须完全一致。

局域网接口为：

```text
http://电脑IPv4地址:8787/sms
```

局域网投递必须使用与 Windows 端完全一致的令牌。Windows 端可以通过“测试连通性”确认服务已启动。

Windows「短信设置」已按电脑接收、手机连接与处理、ntfy、过滤规则和配置管理分区；较窄窗口标签会显示在输入框上方，按钮自动换行。邮箱是电脑端的可选独立通道，在「邮箱设置」查看顶部实时状态及连接 / 规则 / 高级设置。

## root 脚本

### 配置

编辑 [`forward.sh`](forward.sh) 顶部变量：

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
```

只使用局域网时，填写 `PC_IP`、`PC_PORT` 和 `TOKEN` 即可。要让短信同时发到第二台电脑，再填写 `PC2_IP`、`PC2_PORT` 和 `PC2_TOKEN`；`PC2_IP` 留空表示不启用第二台。需要外网转发时，再填写 ntfy 服务器、主题和令牌。

脚本转发验证码短信，电脑记录页可查看原文。

通过设备的 root 服务机制运行，需要短信读取权限与 `curl`。希望开机自动运行可使用 [Magisk 模块](magisk/README.md)。

## Magisk 模块

从 [下载页](https://github.com/Murciee/codepass/releases/latest) 获取 `codepass-sms-magisk.zip`，在 Magisk 中从本地安装，配置后重启手机。

完整步骤见 [`magisk/README.md`](magisk/README.md)。

## LSPosed 模块

LSPosed 方案通过 hook `com.android.phone` 的短信分发流程读取新短信，检测到验证码后转发短信原文。它适合熟悉 LSPosed 的用户，不同 Android 版本和厂商 ROM 可能不兼容。

安装和启用步骤见 [`lsposed/README.md`](lsposed/README.md)。

## ntfy 公网转发

当手机和电脑不在同一网络时，可以让手机将短信发送到 ntfy，Windows 端订阅同一主题。公共服务或自建服务的配置见 [`ntfy.md`](ntfy.md)。

```sh
NTFY_SERVER="https://ntfy.example.com"
NTFY_TOPIC="长随机主题"
NTFY_TOKEN="ntfy Bearer token"
```

## 常见问题

### 锁屏后停止转发

为相关应用关闭电池优化，允许自启动和后台活动，并在最近任务中锁定。部分厂商还需要允许后台联网。

### 电脑没有收到验证码

检查电脑地址、端口和令牌；确认 Windows 防火墙允许 `8787/TCP`；再使用 Windows 端“测试连通性”。

Windows 记录页支持搜索和短信/邮件筛选，默认 10 分钟自动清理，`0` 停用。界面见 [功能截图](../README.md#功能截图)。

### 旧短信被重复发送

root 脚本和 Magisk 模块首次运行时会记录当前短信 ID，正常情况下不会发送启动前的旧短信。若日志显示重复，检查是否同时运行了多个转发方案。

### 日志或配置泄露

日志和配置可能包含服务器地址、主题或令牌，不要上传到公开仓库，也不要发送给不可信的第三方。

排障可使用电脑端“关于 → 预览脱敏诊断”，不要公开完整配置或日志。
