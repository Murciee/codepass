# ntfy 自建教程

本教程使用 Docker 在一台 Linux VPS 或家用服务器上部署 ntfy，并使用 Caddy 自动申请 HTTPS 证书。

如果只是想先试用，不需要自建服务器，直接使用 `https://ntfy.sh` 即可。自建的好处是短信内容不经过公共 ntfy 服务，服务器和数据都由自己控制。

## 准备工作

需要：

1. 一台能长期联网的 Linux 服务器；
2. 一个已经解析到服务器公网 IP 的域名，例如 `push.example.com`；
3. 服务器可以使用 Docker Compose；
4. 防火墙放行 TCP 80 和 443。

下面命令以 Ubuntu/Debian 为例，并假设域名是 `push.example.com`。请把示例域名替换成自己的域名。

## 第一步：安装 Docker

如果服务器已经安装 Docker，可以跳过这一步。

```sh
curl -fsSL https://get.docker.com | sh
sudo systemctl enable --now docker
sudo apt-get install -y docker-compose-plugin
```

确认安装成功：

```sh
docker --version
docker compose version
```

## 第二步：创建 ntfy 配置

```sh
sudo mkdir -p /opt/ntfy/cache
sudo chown -R "$USER":"$USER" /opt/ntfy
cd /opt/ntfy
```

创建 `docker-compose.yml`：

```yaml
services:
  ntfy:
    image: binwiederhier/ntfy:latest
    container_name: codepass-ntfy
    command: serve
    restart: unless-stopped
    environment:
      NTFY_BASE_URL: https://push.example.com
      NTFY_CACHE_FILE: /var/cache/ntfy/cache.db
      NTFY_AUTH_FILE: /var/cache/ntfy/user.db
      NTFY_AUTH_DEFAULT_ACCESS: deny-all
      NTFY_ENABLE_LOGIN: "true"
    volumes:
      - ./cache:/var/cache/ntfy
    ports:
      - "127.0.0.1:2586:80"
```

把 `https://push.example.com` 改成自己的域名，然后启动：

```sh
docker compose up -d
docker compose logs --tail=50 ntfy
```

如果日志没有报错，ntfy 已经在本机的 `127.0.0.1:2586` 监听。此时还不能从公网访问，需要配置 HTTPS 反向代理。

## 第三步：配置 Caddy HTTPS

安装 Caddy，或使用服务器发行版提供的 Caddy 包。然后创建 `/etc/caddy/Caddyfile`：

```caddyfile
push.example.com {
    reverse_proxy 127.0.0.1:2586
}
```

把域名改成自己的域名，再重启 Caddy：

```sh
sudo systemctl enable --now caddy
sudo systemctl reload caddy
```

Caddy 会自动申请并续期 HTTPS 证书。浏览器打开 `https://push.example.com`，能看到 ntfy 页面就说明反向代理成功。

如果不使用 Caddy，也可以使用 nginx、Traefik 或云厂商的 HTTPS 反向代理，但必须确保外部访问地址是 HTTPS。

## 第四步：创建 ntfy 用户和令牌

创建一个管理员用户。命令执行后会要求输入密码：

```sh
docker compose exec ntfy ntfy user add --role=admin codepass
```

创建一个普通用户，用于 codepass 手机和 Windows 端：

```sh
docker compose exec ntfy ntfy user add codepass-client
```

为这个用户允许一个主题读写。主题建议使用长随机字符串，例如 `codepass-7d2e4a9c1f8b`：

```sh
docker compose exec ntfy ntfy access codepass-client codepass-7d2e4a9c1f8b rw
```

为该用户创建访问令牌：

```sh
docker compose exec ntfy ntfy token add codepass-client
```

命令输出的 `tk_...` 字符串就是访问令牌。它只显示一次时，请立即保存到密码管理器，不要放进公开仓库。

## 第五步：测试 ntfy

把下面命令中的域名、主题和令牌替换成自己的值：

```sh
curl -H "Authorization: Bearer tk_你的令牌" \
  -d "654321" \
  https://push.example.com/codepass-7d2e4a9c1f8b
```

如果返回成功响应，说明发布端正常。也可以在 ntfy 手机 App 或网页中订阅同一个主题，确认能看到测试消息。

## 第六步：填写 codepass

Windows 设置：

```text
服务器地址 = https://push.example.com
主题名称   = codepass-7d2e4a9c1f8b
访问令牌   = tk_你的令牌
```

手机端 root 脚本或 Magisk 配置：

```sh
NTFY_SERVER="https://push.example.com"
NTFY_TOPIC="codepass-7d2e4a9c1f8b"
NTFY_TOKEN="tk_你的令牌"
```

保存 Windows 配置后重启 codepass。手机配置修改后重启 Magisk 模块或重启手机。

## 安全检查

- `NTFY_AUTH_DEFAULT_ACCESS` 使用了 `deny-all`，没有账号和令牌的请求默认被拒绝；
- 只给 `codepass-client` 分配一个主题的读写权限；
- 只使用 HTTPS，不要把 token 放在 URL 查询参数中；
- 服务器的 `cache` 目录包含消息缓存，应限制文件权限；
- 不要把真实账号、密码或令牌提交到 GitHub；
- 如果令牌泄露，立即删除旧令牌，再创建新令牌。

## 无域名时的临时测试

可以先让 ntfy 监听服务器公网端口进行测试，但不建议长期这样使用，因为没有 HTTPS 时令牌和短信内容可能被窃听。正式使用应配置域名和 HTTPS。
