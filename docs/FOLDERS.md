# 目录约定（哪放什么，一眼可辨）

> 目的：把"源码 / 当前构建 / 构建过程 / 发布版本 / 改动备份 / 历史归档"分开，避免混在一起。
> 最后整理：2026-10-13（windows\build 已完成产物移入 `archive\win-build\`，评审候选移入 `archive\release-previews\`，改动备份按日期归组；move 清单见 `archive\_move-manifest-20261013.txt`）。

## 顶层一览

| 目录 | 身份 | 放什么 | 不放什么 |
| --- | --- | --- | --- |
| `windows\` | 桌面端源码 | `src\`、`tests\`、`build.ps1`、`config.ini` | 历史构建产物、发布包 |
| `phone\` | 安卓端源码 | `lsposed\`（源码 + gradle 脚本）、`magisk\`、说明文档 | Gradle 构建输出（`app\build\`、`.gradle\` 可随时重新生成） |
| `assets\` | 仓库用图 | README 截图、图标源文件（`codepass-sms.*`、`ui-overview.png`） | 发布包 |
| `docs\` | 文档 | 发布说明、校验报告、构建/发布清单 | 构建产物 |
| `tools\` | 维护脚本 | `draw_icon.py`、`check-public-release.ps1`、`ui-checks\`（界面截图/量测脚本） | — |
| `windows\dist\` | **当前可运行程序（唯一）** | `codepass.exe`、`config.ini`、`history.txt`、`app.log` | 任何带日期的旧版本目录 |
| `windows\build\` | **构建/回归工作区（唯一）** | 正在跑的构建与测试产物；可清空重来 | 已完成、要长期保留的东西 |
| `release\` | **对外发布版（唯一）** | 每个版本一个目录：`v1.0.3\` … `v1.0.9\`；未发布候选放 `_staging\` | 散落根目录的包；旧评审候选包（归档至 `archive\release-previews\`） |
| `backups\` | **改动前备份（唯一）** | 按日期分组：`backups\2026-10-09\`、`backups\2026-10-10\`… | 源码、构建产物 |
| `archive\` | **历史归档（唯一，已 gitignore）** | `win-dist\`（旧 dist 快照）、`win-build\`（旧 build 产物）、`release-previews\`（旧评审候选包）、`gradle\`（Gradle 残留） | 还需要日常使用的东西 |
| `.openchamber\` | 工具生成 | 截图缓存 | — |

## 构建/发布约定（脚本写死的路径，别改）

- `windows\build.ps1`：默认输出 `windows\dist\codepass.exe` + `dist\build-info.json`，并把便携包打包到
  `release\v<版本>\codepass-windows.zip`（2026-10-11 起；`<版本>` 取自 `src\Updater.cs` 的 `AppVersion`）。
  需要输出到别处时用 `-OutputDirectory` / `-ReleaseDirectory` 覆盖。
- `windows\tests\*.ps1`：默认找 `windows\dist\codepass.exe`，临时夹具写在 `windows\build\`。
- 发布版本以 `release\vX.Y.Z\` 为准；每个包里含 `build-info.json`（`version`/`commit`/`exeSha256`）可自证版本。
  旧包（`v1.0.3`）没有 `build-info.json`，版本为依据发布记录推定。

## 归档与恢复

- 归档一律用**移动**，不删除；每次整理写清单：`archive\_move-manifest-<日期>.txt`（含 源 → 目标）。
- 要恢复某个历史构建：按清单把 `archive\...` 里的目录移回原位即可。
