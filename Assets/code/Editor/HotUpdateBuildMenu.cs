#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>旧 AssetBundle 热更新菜单（已停用，改由 GameServer 网页后台发布客户端整包）。</summary>
public static class HotUpdateBuildMenu
{
    [MenuItem("Tools/Quest System/构建并发布全部热更新资源到 GameServer")]
    public static void BuildAndPublishAll()
    {
        EditorUtility.DisplayDialog(
            "已改用网页后台发布",
            "请启动 GameServer 后访问管理后台上传客户端 zip：\nhttp://服务器IP:8765/admin/",
            "确定");
    }
}
#endif
