# CUHKSZ Open World（CUHKSZ-explorer）

香港中文大学（深圳）校园开放世界探索原型，使用 **团结引擎 / Unity** 开发。

## 仓库说明

本仓库只托管 **代码、场景、预制体、配置与 meta 元数据**（约 220 MB）。
**模型、贴图、音频等大体积二进制资产不在仓库内**，见 [`ASSETS_EXTERNAL.md`](ASSETS_EXTERNAL.md)。

## 目录结构

| 路径 | 内容 |
|------|------|
| `Assets/code/` | 游戏逻辑、UI、任务、战斗、编辑器工具 |
| `Assets/scene/` | 主要场景（`.unity`） |
| `Assets/Settings/`、`ProjectSettings/` | 渲染与工程配置 |
| `Packages/` | 包依赖 |
| `Tools/` | Python 辅助工具（剧情/音频处理） |
| `egg/design_docs/` | 设计文档 |

## 本地运行

1. 用团结引擎打开本目录（首次导入会生成 `Library/`，约数十分钟）。
2. 按 `ASSETS_EXTERNAL.md` 从外部存储补齐模型/贴图/音频。
3. 打开 `Assets/scene/Loadingscene.scene` 运行。

## 构建

`File → Build Settings`，输出文件名应为 `CUHKSZ-explorer.exe`（由 Player Settings 的 Product Name 决定）。

## 版权与素材声明

仓库内不含第三方素材的原始文件（模型、贴图、音频），仅保留引用与元数据。
素材版权归各自作者/发行方所有，使用前请遵守对应 Asset Store 许可。