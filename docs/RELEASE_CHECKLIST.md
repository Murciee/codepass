# 发布前检查清单

本清单用于把项目发布到 GitHub 之前逐项确认。项目是个人开源工具，目标不是“商业级完备”，而是**不出低级错误、不误导使用者、不泄露隐私**。

完成检查后可运行脚本辅助核对：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File tools\check-public-release.ps1
```

脚本只做本地只读检查，不联网、不上传、不输出任何敏感内容。

## 一、法律与说明文件

- [ ] 根目录 `LICENSE` 存在，且确为选定的许可证（当前为 MIT）；
- [ ] `DISCLAIMER.md` 存在，明确说明“不承诺售后、不排除法定责任”；
- [ ] `PRIVACY.md` 存在，内容与实际数据处理行为一致；
- [ ] `THIRD_PARTY_NOTICES.md` 存在，列明依赖与运行库；
- [ ] `SECURITY.md` 存在，给出私下报告渠道；
- [ ] `SUPPORT.md` 存在，说明维护边界；
- [ ] README 顶部有清晰的风险提示，并链接到上述文件。

## 二、隐私与默认值核对

- [ ] 隐私说明与代码实际行为一致（记录条数、正文摘要、密码锁不加密、剪贴板清理默认关闭）；
- [ ] 没有把“绝对安全”“完全本地”“绝不泄露”等不实表述写进文档或界面；
- [ ] ntfy 的风险提示准确，未把公共主题名描述成可靠的访问控制。

## 三、敏感信息检查

- [ ] 仓库中不存在真实 `config.ini`、`history.txt`、`*.log`；
- [ ] 仓库中不存在 `*.keystore`、`*.jks`、`key.properties`、`*.p12`、`*.pfx`；
- [ ] 仓库中不存在真实 `lsposed.conf`、`config.conf`；
- [ ] 示例配置 `phone/lsposed/config.example`、`phone/magisk/config.example` 中的令牌为空占位；
- [ ] 源码与文档中没有硬编码的个人 IP、令牌、代理或本机路径；
- [ ] `.gitignore` 已覆盖运行时数据、签名材料、构建输出与本地预览图。

## 四、构建产物检查

- [ ] Windows 包已确认签名状态，并在发布说明中如实标注（当前为**未签名**）；
- [ ] Android Release 包：**未签名包不作为普通用户安装包**；如需分发已签名包，确认使用长期固定证书；
- [ ] 对外发布物（GitHub Release 上传内容）中不包含 Debug APK、调试符号或开发环境路径信息；
- [ ] Magisk 模块打包使用白名单，不混入 `config.conf`、日志或本地文件；
- [ ] 发布包内容与各 `README`（`windows/`、`phone/`、[`docs/BUILD_ANDROID.md`](BUILD_ANDROID.md)）中描述的文件清单一致；
- [ ] `release/` 仅放当前正式附件：`codepass-windows.zip`、`codepass-sms-magisk.zip`、签名验证通过的 `codepass-lsposed-release.apk`；Debug 与 unsigned APK 留在开发构建目录。
- [ ] 为附件生成 `SHA256SUMS.txt`，记录 APK 的公开签名证书 SHA-256 指纹，证书口令及私钥不进入仓库。

## 五、行为与承诺核对

- [ ] 文档中描述的功能与当前代码一致；
- [ ] 未宣传不具备的能力（持久化补发、端到端加密、全平台兼容、正式签名）；
- [ ] 未要求用户关闭杀毒软件或系统安全功能；
- [ ] 版本号与 Android `app/build.gradle`、`phone/magisk/module.prop`、`windows/src/Updater.cs` 的 `AppVersion`、发布说明一致；升级使用更大的 `versionCode`。

## 六、发布动作

- [ ] 选择公开发布渠道（GitHub Release 建议先标记为 **Pre-release**）；
- [ ] 发布前将 `windows/src/Updater.cs` 的 `RepoUrl` 占位（`USERNAME`）替换为实际仓库地址，并验证“关于 → 检查更新”可用；
- [ ] Release 使用 `vX.Y.Z` tag。“检查更新”有新版本时打开固定仓库下载页，用户核对校验值、退出程序后手动覆盖 EXE；程序不再自动下载或执行未验证的更新。
- [ ] 未设置 Windows 令牌时只监听回环；局域网必须使用随机令牌，错误/缺失令牌返回 403。绑定地址/端口或令牌有无修改后重启测试。
- [ ] ntfy 的 HTTP 地址被拒绝；LSPosed 清空目标后确实停止转发，普通应用不能通过 Provider 读取配置。
- [ ] 发布说明写明已测试与未测试的环境；
- [ ] 上传的是**源码**与**构建产物**，不包含任何本地数据；
- [ ] 首次发布后，检查仓库首页是否正确显示许可证；
- [ ] 确认 Issue 模板或 README 已提醒“请勿粘贴真实短信与令牌”。

## 发布后

- [ ] 记录本次发布的已知问题；
- [ ] 若发现严重影响使用的安全或隐私问题，及时在仓库发布提示，或暂停提供受影响版本；
- [ ] 后续更新时沿用 Android 相同的签名证书，否则用户无法覆盖升级。
