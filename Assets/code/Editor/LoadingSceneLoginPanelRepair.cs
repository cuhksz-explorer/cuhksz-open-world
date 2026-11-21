#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 仅修复 Loadingscene 的 GameServer_LoginPanel：GUID 对齐、重建 LGUHUB 子 UI、清理孤立页。
/// 不扫描/修改 undergarden 或其他场景。
/// </summary>
public static class LoadingSceneLoginPanelRepair
{
    const string MenuPath = "Tools/Game Server/Repair Loadingscene Login Panel";
    const string ScenePath = "Assets/scene/Loadingscene.scene";

    static readonly (string OldGuid, string NewGuid)[] LoadingsceneGuidRepairs =
    {
        ("f89fb456cf42d624691686107805acf6", "d496bc45f5634777baf1ebd19036ce65"), // PlayerCharacterRegisterUI
        ("fcc8e3731c2290041a6040f113eda9a9", "e227f7cf30214fa9a6a23654b1a180f3"), // PlayerCharacterCatalog
        ("973e42bdcc6963c469edb78785f4ee53", "3b1df9cf3bd64aff9dd5b8301f16ec48"), // PlayerCharacterApplier
    };

    [MenuItem(MenuPath, false, 2)]
    public static void RepairFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Loadingscene 登录面板", "请先退出 Play Mode。", "确定");
            return;
        }

        int guidFixes = RepairLoadingsceneGuidsOnDisk();
        if (guidFixes > 0)
            AssetDatabase.Refresh();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        int orphans = RemoveOrphanedLoginUi(scene);
        int rebuilt = RebuildLoginPanel(scene);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        EditorUtility.DisplayDialog(
            "Loadingscene 登录面板",
            $"GUID 替换：{guidFixes} 处\n" +
            $"清理孤立 UI：{orphans} 个\n" +
            $"登录面板：{(rebuilt > 0 ? "已重建" : "未找到 GameServerAuthUI")}\n\n" +
            "仅修改了 Loadingscene，未触碰 undergarden。",
            "确定");
    }

    public static int RepairLoadingsceneGuidsOnDisk()
    {
        string path = Path.Combine(Application.dataPath, "scene/Loadingscene.scene");
        if (!File.Exists(path))
            return 0;

        string text = File.ReadAllText(path);
        string original = text;
        int count = 0;

        foreach (var (oldGuid, newGuid) in LoadingsceneGuidRepairs)
        {
            if (!text.Contains(oldGuid))
                continue;
            int replaced = 0;
            int index = 0;
            while ((index = text.IndexOf(oldGuid, index, System.StringComparison.Ordinal)) >= 0)
            {
                text = text.Remove(index, oldGuid.Length).Insert(index, newGuid);
                index += newGuid.Length;
                replaced++;
            }
            count += replaced;
        }

        // 缺失的 BGM 资源：解除引用，避免 Inspector 显示 Missing
        const string missingBgm = "6eef7c45b78f87540a285aad1d61a1ce";
        if (text.Contains(missingBgm))
        {
            text = text.Replace(
                "{fileID: 8300000, guid: " + missingBgm + ", type: 3}",
                "{fileID: 0}");
            count++;
        }

        if (text == original)
            return 0;

        File.WriteAllText(path, text);
        Debug.Log($"[LoadingSceneLoginPanelRepair] {ScenePath}：已更新 {count} 处 GUID/资源引用。");
        return count;
    }

    static int RemoveOrphanedLoginUi(Scene scene)
    {
        var panel = FindLoginPanel(scene);
        int removed = 0;
        var roots = scene.GetRootGameObjects();
        var toDestroy = new List<GameObject>();

        foreach (var root in roots)
        {
            if (root == null)
                continue;
            CollectOrphanedLoginUi(root.transform, panel != null ? panel.transform : null, toDestroy);
        }

        foreach (var go in toDestroy)
        {
            if (go == null)
                continue;
            Undo.DestroyObjectImmediate(go);
            removed++;
        }

        return removed;
    }

    static void CollectOrphanedLoginUi(Transform node, Transform panelRoot, List<GameObject> output)
    {
        if (node == null)
            return;

        if (panelRoot != null && (node == panelRoot || node.IsChildOf(panelRoot)))
            return;

        string name = node.name;
        if (name == "LoginPage" || name == "RegisterPage" || name == "PanelHeader" || name == "PanelFooter")
        {
            output.Add(node.gameObject);
            return;
        }

        for (int i = node.childCount - 1; i >= 0; i--)
            CollectOrphanedLoginUi(node.GetChild(i), panelRoot, output);
    }

    static int RebuildLoginPanel(Scene scene)
    {
        var auth = FindLoginPanel(scene);
        if (auth == null)
            return 0;

        var sync = Object.FindObjectOfType<GameServerSync>(true);
        if (sync == null)
        {
            Debug.LogWarning("[LoadingSceneLoginPanelRepair] 未找到 GameServerSync，登录逻辑可能无法保存。");
        }

        Undo.RegisterFullObjectHierarchyUndo(auth.gameObject, "Repair Loadingscene login panel");

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

        auth.gameObject.SetActive(false);

        var flow = Object.FindObjectOfType<MenuLoginPortalFlow>(true);
        if (flow != null)
        {
            var fso = new SerializedObject(flow);
            fso.FindProperty("loginPanelRoot").objectReferenceValue = auth.gameObject;
            fso.ApplyModifiedPropertiesWithoutUndo();
        }

        WirePlayerCharacterComponents(flow, sync);

        Selection.activeGameObject = auth.gameObject;
        EditorGUIUtility.PingObject(auth.gameObject);
        return 1;
    }

    static void WirePlayerCharacterComponents(MenuLoginPortalFlow flow, GameServerSync sync)
    {
        var catalog = AssetDatabase.LoadAssetAtPath<PlayerCharacterCatalog>(
            PlayerCharacterEditorShared.CatalogPath);
        if (catalog == null)
            return;

        if (flow != null)
        {
            var ui = flow.GetComponent<PlayerCharacterRegisterUI>();
            if (ui == null)
                ui = flow.gameObject.AddComponent<PlayerCharacterRegisterUI>();
            var uiSo = new SerializedObject(ui);
            uiSo.FindProperty("catalog").objectReferenceValue = catalog;
            uiSo.ApplyModifiedPropertiesWithoutUndo();
        }

        if (sync != null)
        {
            var applier = sync.GetComponent<PlayerCharacterApplier>();
            if (applier == null)
                applier = sync.gameObject.AddComponent<PlayerCharacterApplier>();
            var apSo = new SerializedObject(applier);
            apSo.FindProperty("catalog").objectReferenceValue = catalog;
            apSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static GameServerAuthUI FindLoginPanel(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var auth = root.GetComponentInChildren<GameServerAuthUI>(true);
            if (auth != null && auth.gameObject.name == "GameServer_LoginPanel")
                return auth;
        }

        return Object.FindObjectOfType<GameServerAuthUI>(true);
    }
}
#endif
