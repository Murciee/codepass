# codepass Magisk 模块

Magisk 模块在手机启动后以 root 权限读取收件箱中的新短信，检测到验证码后把短信原文发送到 codepass Windows 端（验证码由电脑端识别）。它适合已经使用 Magisk、希望后台自动运行的用户。

## 安装

在项目根目录的 Windows PowerShell 中构建模块：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File phone\magisk\build.ps1
```

构建产物：

```text
release/codepass-sms-magisk.zip
```

将 ZIP 传到手机，在 Magisk 应用中选择“从本地安装”，安装完成后再重启手机。

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

模块检测到验证码后会把短信原文发送到电脑，验证码由 Windows 端识别；记录页的“原文”为收到的短信内容（超过 120 字符会截断，换行转为空格）。

修改配置后必须重启手机，使后台服务重新加载设置。WebUI 显示“已保存”仅表示配置写入完成，不代表运行中的服务已应用新配置。

## WebUI

模块包含 `webroot/index.html`。支持模块 WebUI 的管理器可以使用图形页面编辑配置。部分环境需要安装 **WebUI X** 并启用 `WXU` 文件系统插件。

如果管理器不支持 WebUI，模块仍可正常运行，直接编辑 `config.conf` 即可。WebUI 和手动编辑使用同一个配置文件，不会产生两套配置。

页面使用与 Windows、LSPosed 一致的柔和蓝灰配色和大圆角卡片，窄屏自动改为单列，并适配设备安全区。键盘操作有焦点提示，保存结果会在页面中显示。

CSS 动画由浏览器按实际渲染节奏调度，不设置固定 60 fps 循环；开启系统减少动画时停用过渡效果。实际刷新率取决于 WebUI 宿主和 ROM。界面动画与 `INTERVAL` 短信轮询间隔无关，后者不需要为高刷而缩短。

## 日志和验证

运行日志使用 `codepass.log`，可通过具有相应权限的管理器查看。公开诊断信息前应删除短信及访问令牌等敏感内容。

检查顺序：

1. 确认 Windows 端已启动并允许手机访问；
2. 确认 `PC_IP`、端口和令牌正确；启用第二台电脑时同时确认 `PC2_*`；
3. 使用 Windows 端“测试连通性”；
4. 检查服务启动及验证码检测日志，并通过测试短信与电脑记录确认送达；
5. 确认没有同时运行 root 脚本或其他短信转发应用。

模块首次运行时只记录当前最新短信 ID，正常情况下不会把启动前的旧短信全部发送。

日志中的验证码检测不代表发送成功。当前每次查询仅处理最新一条短信，并在发送前推进短信 ID；突发短信、网络失败或重启可能造成漏发，没有持久化补发保证。

## 兼容性和限制

- 不同厂商 ROM 对短信数据库和后台网络的限制不同；
- 如果 `content query` 无法读取短信，建议改用免 root Webhook；
- 部分系统需要为 Magisk 和相关服务关闭电池优化；
- 配置文件包含访问令牌，应限制 root 文件访问，不要上传到公开仓库。
