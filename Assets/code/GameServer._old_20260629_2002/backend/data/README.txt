运行时数据已迁移到 Unity 项目根目录的 GameServerRuntime/ 文件夹。

请勿在此目录存放客户端 Build 产物（含 Managed/*.dll），否则 Unity 会将其当作插件导入并报错。

启动服务器时会自动从本目录迁移历史数据（若存在）。
