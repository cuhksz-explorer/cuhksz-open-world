#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 将 Loadingscene 的 GameServer_LoginPanel 改为 LGUHUB 风格（白底黑字、金色登录、独立注册页）。
/// </summary>
public static class LoadingSceneLguHubLoginUISetup
{
    const string MenuPath = "Tools/Game Server/Apply LGUHUB Style To Loadingscene Login";
    const string ScenePath = "Assets/scene/Loadingscene.scene";

    [MenuItem(MenuPath, false, 3)]
    public static void ApplyToLoadingscene()
    {
        LoadingSceneLoginPanelRepair.RepairLoadingsceneGuidsOnDisk();
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var auth = Object.FindObjectOfType<GameServerAuthUI>(true);
        if (auth == null)
        {
            EditorUtility.DisplayDialog(
                "LoadingScene 登录 UI",
                "未在 Loadingscene 中找到 GameServerAuthUI。\n请先运行 Tools/Game Server/Create Login UI (One Click)。",
                "确定");
            return;
        }

        var sync = Object.FindObjectOfType<GameServerSync>(true);
        if (sync == null)
        {
            EditorUtility.DisplayDialog("LoadingScene 登录 UI", "未找到 GameServerSync。", "确定");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(auth.gameObject, "Apply LGUHUB login UI");
        var so = new SerializedObject(auth);
        so.FindProperty("useLguHubTwoPageLayout").boolValue = true;
        so.FindProperty("hideLogoutButton").boolValue = true;
        so.FindProperty("loginPageRoot").objectReferenceValue = null;
        so.FindProperty("registerPageRoot").objectReferenceValue = null;
        so.FindProperty("usernameInput").objectReferenceValue = null;
        so.FindProperty("passwordInput").objectReferenceValue = null;
        so.FindProperty("loginButton").objectReferenceValue = null;
        so.FindProperty("goToRegisterButton").objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();

        LguHubAuthUILayout.ApplyToPanel(auth.transform, auth, sync);

        var flow = Object.FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow != null)
        {
            var fso = new SerializedObject(flow);
            fso.FindProperty("loginPanelRoot").objectReferenceValue = auth.gameObject;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = auth.gameObject;
        EditorGUIUtility.PingObject(auth.gameObject);
        Debug.Log("[GameServer] 已应用 LGUHUB 风格登录/注册 UI 到 Loadingscene。");
    }
}
#endif
