# 手机端部署指南

codepass 手机端负责读取新短信并发送到 Windows 电脑；root 脚本、Magisk 模块与 LSPosed 模块在检测到验证码后转发短信原文，验证码由 Windows 端识别。根据手机权限环境选择一种方案即可，不需要同时安装多个方案。

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

脚本在检测到验证码后会把短信原文发送到电脑，验证码由 Windows 端识别；记录页的“原文”为收到的短信内容（超过 120 字符会截断，换行转为空格）。

### 部署方式

root 脚本需要由设备的 root 服务机制运行，部署位置和权限应根据所用管理器确定。普通用户建议使用提供安装和开机服务管理的 [Magisk 模块](magisk/README.md)，避免手工配置系统服务。

手工部署者应阅读脚本内的配置定义，在受控环境验证短信读取、网络连接和开机行为。日志使用 `smscode.log`，不得公开其中的敏感内容。

### 环境检查

```sh
content query --uri content://sms/inbox --projection _id:body --limit 1
which curl
```

如果无法读取短信数据库，或系统没有 `curl`，建议改用免 root Webhook 或 Magisk 模块。

## Magisk 模块

在电脑上构建：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\magisk\build.ps1
```

生成 `codepass-sms-magisk.zip` 后，在 Magisk 中选择“从本地安装”。模块使用 `config.conf` 保存设置，使用 `codepass.log` 记录运行状态；优先通过支持的模块管理器操作。

完整步骤见 [`magisk/README.md`](magisk/README.md)。

## LSPosed 模块

LSPosed 方案通过 hook `com.android.phone` 的短信分发流程读取新短信，检测到验证码后转发短信原文。它适合熟悉 LSPosed 的用户，不同 Android 版本和厂商 ROM 可能不兼容。

构建和启用步骤见 [`lsposed/README.md`](lsposed/README.md)。

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

### 旧短信被重复发送

root 脚本和 Magisk 模块首次运行时会记录当前短信 ID，正常情况下不会发送启动前的旧短信。若日志显示重复，检查是否同时运行了多个转发方案。

### 日志或配置泄露

日志和配置可能包含服务器地址、主题或令牌，不要上传到公开仓库，也不要发送给不可信的第三方。
