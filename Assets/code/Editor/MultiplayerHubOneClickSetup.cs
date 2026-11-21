#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 一键配置：<see cref="MultiplayerRoomCodePanelController"/>（K）+ <see cref="MultiplayerFriendChatSidebarController"/>（C）+
/// <see cref="MultiplayerEntryToggle"/>。
/// Unity 菜单: <b>Tools / 联机 / 一键配置联机服务</b>
/// </summary>
public static class MultiplayerHubOneClickSetup
{
    const string MenuPath = "Tools/联机/一键配置联机服务";
    const string ServicesObjectName = "Multiplayer_Services";

    [MenuItem(MenuPath, false, 5)]
    public static void Configure()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("联机", "请在编辑模式下运行一键配置（退出 Play 后再试）。", "确定");
            return;
        }

        Transform preferredParent = null;
        var sync = Object.FindObjectOfType<GameServerSync>();
        if (sync != null)
            preferredParent = sync.transform;

        var room = Object.FindObjectOfType<MultiplayerRoomCodePanelController>(true);
        GameObject hostGo;

        if (room != null)
        {
            hostGo = room.gameObject;
            Undo.RegisterCompleteObjectUndo(hostGo, "Multiplayer one-click");
        }
        else if (sync != null)
        {
            hostGo = sync.gameObject;
            Undo.RegisterCompleteObjectUndo(hostGo, "Multiplayer one-click");
        }
        else
        {
            hostGo = new GameObject(ServicesObjectName);
            Undo.RegisterCreatedObjectUndo(hostGo, "Multiplayer one-click");
            if (preferredParent != null)
                Undo.SetTransformParent(hostGo.transform, preferredParent, "Multiplayer one-click parent");
        }

        var hubLegacy = hostGo.GetComponent<MultiplayerHubController>();
        if (hubLegacy != null)
            Undo.DestroyObjectImmediate(hubLegacy);

        room = hostGo.GetComponent<MultiplayerRoomCodePanelController>();
        if (room == null)
            room = Undo.AddComponent<MultiplayerRoomCodePanelController>(hostGo);

        var soRoom = new SerializedObject(room);
        var propPanel = soRoom.FindProperty("panelRoot");
        if (propPanel != null && propPanel.objectReferenceValue != null)
        {
            if (!EditorUtility.DisplayDialog(
                    "联机一键配置",
                    "当前已指定自定义房间号 Panel Root。\n继续将清空并改为运行时自动生成。\n\n是否继续？",
                    "继续",
                    "取消"))
            {
                Selection.activeGameObject = hostGo;
                return;
            }
        }
        if (propPanel != null)
            propPanel.objectReferenceValue = null;
        var propEnsure = soRoom.FindProperty("ensureRuntimeCanvas");
        if (propEnsure != null)
            propEnsure.boolValue = true;
        var propPort = soRoom.FindProperty("mirrorPort");
        if (propPort != null)
            propPort.intValue = 7777;

        var coopGo = Resources.Load<GameObject>("CoopPlayer");
        var propCoop = soRoom.FindProperty("coopPlayerPrefab");
        if (propCoop != null && coopGo != null)
        {
            var cn = coopGo.GetComponent<CoopNetworkPlayer>();
            propCoop.objectReferenceValue = cn;
            if (cn == null)
                Debug.LogWarning("[联机一键配置] Resources/CoopPlayer 未挂载 CoopNetworkPlayer。请执行 Tools/联机/Create Coop Player Prefab。");
        }
        else if (coopGo == null)
            Debug.LogWarning("[联机一键配置] 未找到 Resources/CoopPlayer。");

        soRoom.ApplyModifiedProperties();

        var sidebar = hostGo.GetComponent<MultiplayerFriendChatSidebarController>();
        if (sidebar == null)
            sidebar = Undo.AddComponent<MultiplayerFriendChatSidebarController>(hostGo);
        var soSide = new SerializedObject(sidebar);
        var sidePanel = soSide.FindProperty("sidebarRoot");
        if (sidePanel != null)
            sidePanel.objectReferenceValue = null;
        var sideEnsure = soSide.FindProperty("ensureRuntimeCanvas");
        if (sideEnsure != null)
            sideEnsure.boolValue = true;
        soSide.ApplyModifiedProperties();

        var toggle = hostGo.GetComponent<MultiplayerEntryToggle>();
        if (toggle == null)
            toggle = Undo.AddComponent<MultiplayerEntryToggle>(hostGo);
        var soToggle = new SerializedObject(toggle);
        var propRoomRef = soToggle.FindProperty("roomPanel");
        if (propRoomRef != null)
            propRoomRef.objectReferenceValue = room;
        var propHotkey = soToggle.FindProperty("enableHotkey");
        if (propHotkey != null)
            propHotkey.boolValue = true;
        var propKey = soToggle.FindProperty("toggleKey");
        if (propKey != null)
            propKey.intValue = (int)KeyCode.K;
        soToggle.ApplyModifiedProperties();

        if (preferredParent == null)
        {
            if (hostGo.GetComponent<MultiplayerServiceRoot>() == null)
                Undo.AddComponent<MultiplayerServiceRoot>(hostGo);
        }
        else
        {
            var root = hostGo.GetComponent<MultiplayerServiceRoot>();
            if (root != null)
                Undo.DestroyObjectImmediate(root);
        }

        EditorUtility.SetDirty(hostGo);
        EditorUtility.SetDirty(room);
        EditorUtility.SetDirty(sidebar);
        EditorUtility.SetDirty(toggle);

        Selection.activeGameObject = hostGo;
        EditorGUIUtility.PingObject(hostGo);

        var msg = "已配置：\n" +
                  "• MultiplayerRoomCodePanelController（K：房间代码）\n" +
                  "• MultiplayerFriendChatSidebarController（C：好友/联机/聊天侧栏）\n" +
                  "• MultiplayerEntryToggle（K）\n";
        if (sync != null)
            msg += "• 建议在 GameServerSync 同物体上，便于跨场景\n";
        msg += "\n按 K 房间号面板；按 C 或侧栏按钮打开聊天区。需先登录 GameServer。";

        EditorUtility.DisplayDialog("联机一键配置", msg, "确定");
        Debug.Log("[联机] " + MenuPath + " 完成。目标物体：" + hostGo.name);
    }
}
#endif
