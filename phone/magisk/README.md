# codepass Magisk 模块

开机后自动将验证码短信转发到 Windows，支持双电脑和 ntfy。

## 安装

从 [下载页](https://github.com/Murciee/codepass/releases/latest) 获取 `codepass-sms-magisk.zip`，在 Magisk 中从本地安装，配置后重启手机。

## 配置

模块使用 `config.conf` 保存设置，建议通过支持的 WebUI 管理配置。

使用 root 文件管理器或 ADB 修改以下参数：

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
INTERVAL=3
MIN_LEN=4
MAX_LEN=8
```

局域网模式至少填写一台电脑的地址和端口（`PC_IP`/`PC_PORT` 或 `PC2_IP`/`PC2_PORT`），令牌必须与对应 Windows 端一致；Windows 未设置令牌时只监听本机，无法接收手机请求。双电脑填写各自的地址、端口和令牌。ntfy 必须使用 HTTPS，HTTP 配置会停用该通道。

修改配置后重启手机生效。

## WebUI

支持模块 WebUI 的管理器可直接编辑设置；部分环境需要 **WebUI X** 和 `WXU` 文件系统插件。

如果管理器不支持 WebUI，模块仍可正常运行，直接编辑 `config.conf` 即可。WebUI 和手动编辑使用同一个配置文件，不会产生两套配置。

## 排查

检查 Windows 是否运行、地址/端口/令牌、防火墙，以及是否同时启用了其他转发方案；电脑端可使用“测试连通性”。

模块首次运行时只记录当前最新短信 ID，正常情况下不会把启动前的旧短信全部发送。

突发短信、断网或重启可能造成漏发，不保证补发。

Windows 支持短信与邮箱验证码、记录搜索和自动清理，详见 [使用说明](https://github.com/Murciee/codepass)。排障使用“关于 → 预览脱敏诊断”，不要公开配置或日志。

## 兼容性和限制

- 不同厂商 ROM 对短信数据库和后台网络的限制不同；
- 如果 `content query` 无法读取短信，建议改用免 root Webhook；
- 部分系统需要为 Magisk 和相关服务关闭电池优化；
- 配置文件包含访问令牌，应限制 root 文件访问，不要上传到公开仓库。
