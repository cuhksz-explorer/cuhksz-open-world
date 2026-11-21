egg/ — 与游戏运行无关的策划表、备份、报告与文档归档。

不影响 Unity 构建与运行时资源加载。
剧情源文件仍保留在 Assets/content1/（数据库 sourceAssetPath 会引用）。

目录说明：
  workbooks/           策划 Excel（配音文案、资产清单、报价表等）
  design_docs/         任务手册、联机指南、模块 README 等文档
  tools_reports/       配音导入报告、翻译中间 JSON 等
  tools_audio_cache/   音频下载/解压缓存（可删后重新生成）
  editor_reports/      GUID 修复日志
  scene_backups/       历史场景完整备份（原 Assets/scene/Backups）
  library_reimport_backup/  图书馆楼层 prefab 备份
  scene_orphans/       零散 .bak 场景文件
  import_temp/         临时导入包解压目录
  root_misc/           根目录杂项脚本/状态 txt

如需恢复场景备份，从 scene_backups/ 复制回 Assets/scene/Backups/。
配音相关 Tools 脚本已改为优先读取 egg/workbooks/剧情配音文案.xlsx。
