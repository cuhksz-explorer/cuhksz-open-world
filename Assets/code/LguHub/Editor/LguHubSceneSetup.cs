#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 为 libraryfloor1 / libraryfloor2 / libraryfloor4 配置 LGUHUB：
/// - 挂载 P 键原生面板
/// - 重建登录面板（AlibabaPuHui 双页布局）
/// </summary>
public static class LguHubSceneSetup
{
    const string MenuPath = "Tools/场景配置/LGUHUB - 配置图书馆场景（P 键打开）";
    static readonly string[] ScenePaths =
    {
        "Assets/scene/libraryfloor1.scene",
        "Assets/scene/libraryfloor2.scene",
        "Assets/scene/libraryfloor4.scene",
    };

    [MenuItem(MenuPath, false, 45)]
    public static void SetupFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("LGUHUB", "请先退出 Play Mode。", "确定");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        int changed = 0;
        int loginRebuilt = 0;
        var active = SceneManager.GetActiveScene().path;
        foreach (var path in ScenePaths)
        {
            if (!System.IO.File.Exists(path))
                continue;
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool sceneChanged = false;
            if (EnsureRuntimeInScene(scene))
                sceneChanged = true;
            if (RebuildLoginPanel(scene))
            {
                sceneChanged = true;
                loginRebuilt++;
            }
            if (sceneChanged)
            {
                EditorSceneManager.SaveScene(scene);
                changed++;
            }
        }

        if (!string.IsNullOrEmpty(active) && System.IO.File.Exists(active))
            EditorSceneManager.OpenScene(active, OpenSceneMode.Single);

        EditorUtility.DisplayDialog(
            "LGUHUB",
            $"已处理 {changed} 个图书馆场景。\n" +
            $"登录面板重建：{loginRebuilt} 个\n\n" +
            "按 P 打开 LGUHUB 面板（需已登录游戏账号）。",
            "确定");
    }

    static bool EnsureRuntimeInScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        var existing = Object.FindObjectOfType<LguHubPanelController>(true);
        if (existing != null)
        {
            var sync = Object.FindObjectOfType<GameServerSync>(true);
            if (sync != null && existing.transform.parent != sync.transform)
                existing.transform.SetParent(sync.transform, false);
            return false;
        }

        GameObject host;
        var serverSync = Object.FindObjectOfType<GameServerSync>(true);
        if (serverSync != null)
        {
            host = new GameObject("LguHub_Runtime");
            host.transform.SetParent(serverSync.transform, false);
        }
        else
        {
            host = new GameObject("LguHub_Runtime");
        }

        Undo.RegisterCreatedObjectUndo(host, "Add LguHub_Runtime");
        host.AddComponent<LguHubApiClient>();
        host.AddComponent<LguHubPanelController>();

        var toggle = Object.FindObjectOfType<GameServerLoginPanelToggle>(true);
        if (toggle != null)
        {
            var so = new SerializedObject(toggle);
            so.FindProperty("loginPanel").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();
            toggle.enabled = false;
        }

        if (serverSync == null)
            Debug.LogWarning($"[LGUHUB] {scene.name}: 未找到 GameServerSync，请确保场景或 DontDestroyOnLoad 中有 GameServer_Network。");

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[LGUHUB] 已在场景 {scene.name} 挂载 LguHub_Runtime。");
        return true;
    }

    static bool RebuildLoginPanel(Scene scene)
    {
        var auth = Object.FindObjectOfType<GameServerAuthUI>(true);
        if (auth == null)
            return false;

        var sync = Object.FindObjectOfType<GameServerSync>(true);
        Undo.RegisterFullObjectHierarchyUndo(auth.gameObject, "Rebuild library LGUHUB login panel");

        var so = new SerializedObject(auth);
        so.FindProperty("useLguHubTwoPageLayout").boolValue = true;
        so.FindProperty("hideLogoutButton").boolValue = true;
        so.FindProperty("loginPageRoot").objectReferenceValue = null;
        so.FindProperty("registerPageRoot").objectReferenceValue = null;
        so.FindProperty("usernameInput").objectReferenceValue = null;
        so.FindProperty("passwordInput").objectReferenceValue = null;
        so.FindProperty("registerUsernameInput").objectReferenceValue = null;
        so.FindProperty("registerPasswordInput").objectReferenceValue = null;
        so.FindProperty("loginButton").objectReferenceValue = null;
        so.FindProperty("registerSubmitButton").objectReferenceValue = null;
        so.FindProperty("goToRegisterButton").objectReferenceValue = null;
        so.FindProperty("backToLoginButton").objectReferenceValue = null;
        so.FindProperty("closePanelButton").objectReferenceValue = null;
        so.FindProperty("forgotPasswordButton").objectReferenceValue = null;
        so.FindProperty("mobileQuickLoginPlaceholder").objectReferenceValue = null;
        so.FindProperty("agreementToggle").objectReferenceValue = null;
        so.FindProperty("userAgreementLink").objectReferenceValue = null;
        so.FindProperty("privacyPolicyLink").objectReferenceValue = null;
        so.FindProperty("agreementLineClick").objectReferenceValue = null;
        so.FindProperty("panelFooterRoot").objectReferenceValue = null;
        so.FindProperty("statusText").objectReferenceValue = null;
        so.FindProperty("registerStatusText").objectReferenceValue = null;
        if (sync != null)
            so.FindProperty("serverSync").objectReferenceValue = sync;
        so.ApplyModifiedPropertiesWithoutUndo();

        LguHubAuthUILayout.ApplyToPanel(auth.transform, auth, sync);

        var panelImg = auth.GetComponent<Image>();
        if (panelImg != null)
            panelImg.sprite = null;

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"[LGUHUB] {scene.name}: 已重建 GameServer_LoginPanel（AlibabaPuHui 双页布局）。");
        return true;
    }
}
#endif
