# 外部资产清单（不在 Git 仓库内）

本工程完整资产约 **28 GB**，GitHub 单文件限制 100 MB、仓库体积也放不下，因此按类型外置。
仓库里保留了全部 `.meta` 文件，**从外部补齐同名文件后 GUID 不变，引用不会断**。

## 被排除的内容

| 类别 | 扩展名 | 体积 |
|------|--------|------|
| Blender 源文件 | `.blend` | ~9.7 GB（单文件最大 9.8 GB） |
| 模型 | `.fbx` `.glb` `.ply` `.obj` | ~13 GB（单文件最大 3.66 GB） |
| 贴图 | `.png` `.jpg` `.tga` `.exr` `.tif` `.psd` | ~2.5 GB |
| 音频 | `.mp3` `.wav` `.ogg` | ~0.4 GB |
| 高斯泼溅数据 | `.bytes` `.ply` | ~1 GB |
| TMP 超大字体图集 | `AlibabaPuHuiTi-3-75-SemiBold SDF.asset` | 129 MB |
| 服务器运行时 | `GameServerRuntime/` | 18.6 GB（已停用） |
| Unity 缓存 | `Library/` `Temp/` `obj/` `Logs/` `UserSettings/` | 56 GB（可重建） |

## 恢复步骤

1. 从团队网盘 / NAS / 移动硬盘取出完整工程备份。
2. 用 `robocopy` 只复制被排除的资产，避免覆盖仓库代码：

```bat
robocopy "\\NAS\CUHKSZ-explorer\Assets" "%~dp0Assets" /E ^
  /XF *.cs *.unity *.prefab *.asset *.mat *.anim *.meta ^
  /XD Library Temp obj Logs UserSettings .git
```

3. 打开工程，确认场景无粉色材质、模型无丢失。
4. 若要校验完整性：资产目录总大小应约 28 GB。

## 为什么不用 Git LFS

- GitHub LFS 单文件上限 2 GB，本工程有 3.66 GB 与 9.8 GB 的单文件，仍然过不去；
- LFS 免费额度只有 1 GB 存储，全量资产需要付费数据包；
- 结论：大资产用外部存储或 Unity Version Control (UVCS)，GitHub 只做代码版本管理。