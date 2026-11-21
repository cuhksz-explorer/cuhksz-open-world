#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>编辑器：一键把好友聊天画布生成到当前场景，便于用 Rect Transform 手动摆位。</summary>
public static class FriendChatSidebarPanelMenu
{
    const string MenuPath = "Tools/联机/生成好友聊天面板到场景(可手动调位置)";

    [MenuItem(MenuPath)]
    static void MenuGenerateFriendChatPanel()
    {
        var ctrl = Object.FindObjectOfType<MultiplayerFriendChatSidebarController>(true);
        if (ctrl == null)
        {
            var host = GameObject.Find("Multiplayer_Services");
            if (host == null)
            {
                host = new GameObject("Multiplayer_Services");
                Undo.RegisterCreatedObjectUndo(host, "Create Multiplayer_Services");
            }
            ctrl = Undo.AddComponent<MultiplayerFriendChatSidebarController>(host);
            Debug.Log("[好友聊天] 当前场景未找到侧栏控制器，已自动创建 Multiplayer_Services 并挂载 MultiplayerFriendChatSidebarController。");
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("好友聊天", "请先退出 Play 再生成面板（生成结果写入场景）。", "确定");
            return;
        }

        Undo.SetCurrentGroupName("生成好友聊天面板");
        int gid = Undo.GetCurrentGroup();

        foreach (var cv in Object.FindObjectsOfType<Canvas>(true))
        {
            if (cv == null || cv.name != "FriendChatSidebarCanvas") continue;
            if (cv.gameObject.scene != ctrl.gameObject.scene) continue;
            Undo.DestroyObjectImmediate(cv.gameObject);
            break;
        }

        Undo.RecordObject(ctrl, "Friend Chat Panel");
        ctrl.BuildUiForSceneEditing();
        EditorUtility.SetDirty(ctrl);
        if (ctrl.gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(ctrl.gameObject.scene);

        Undo.CollapseUndoOperations(gid);
        Selection.activeGameObject = ctrl.gameObject;
        Debug.Log("[好友聊天] 已生成 FriendChatSidebarCanvas。请在 Hierarchy 中选中间布或 FriendChatSidebar，用 Rect Transform 调整位置与大小；已关闭「运行时自动侧栏布局」。");
    }
}
#endif
