#if UNITY_EDITOR
using System;
using DormMall;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 寝室往返传送：为进入寝室的 TeleportButton 设入口模式与返回锚点；在 dorm 场景创建离开按钮。
/// </summary>
public static class DormTeleportEditorMenu
{
    const string AddAnchorsMenu = "Tools/DormMall/为「进入寝室」传送门添加返回锚点";
    const string CreateExitMenu = "Tools/DormMall/在 dorm 场景创建离开寝室按钮";
    const string AddExitToOpenMenu = "Tools/DormMall/在当前场景选中物体上配置离开寝室（TeleportButton）";
    const string FixCollegeACameraMenu = "Tools/DormMall/修复 lingcollegeA 相机默认状态（第三人称关）";
    const string AddCollegeADormPortalMenu = "Tools/DormMall/为 lingcollegeA 配置进入寝室传送门";

    [MenuItem(AddCollegeADormPortalMenu)]
    static void ConfigureLingCollegeADormPortal()
    {
        const string scenePath = "Assets/scene/lingcollegeA.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
        {
            EditorUtility.DisplayDialog("寝室传送", "未找到 lingcollegeA 场景。", "确定");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int configured = 0;
        foreach (var transfer in UnityEngine.Object.FindObjectsOfType<TeleportButton>(true))
        {
            if (transfer == null)
                continue;
            string target = transfer.targetSceneName ?? "";
            if (!target.Contains("dorm", System.StringComparison.OrdinalIgnoreCase))
                continue;

            Undo.RecordObject(transfer, "Configure lingcollegeA dorm portal");
            transfer.targetSceneName = "scene/dorm";
            transfer.dormRole = DormTeleportRole.EnterDorm;
            if (transfer.GetComponent<DormReturnAnchor>() == null)
                Undo.AddComponent<DormReturnAnchor>(transfer.gameObject);
            EditorUtility.SetDirty(transfer);
            configured++;
        }

        if (configured == 0)
        {
            EditorUtility.DisplayDialog(
                "寝室传送",
                "lingcollegeA 中未找到目标含 dorm 的 TeleportButton。\n请先在场景中复制 lingcollegeB 的「进入寝室」按钮，将 targetSceneName 设为 scene/dorm 后再运行本菜单。",
                "确定");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog(
            "寝室传送",
            $"已为 {configured} 个传送门设置 EnterDorm + DormReturnAnchor。\n请保存场景后测试 A→寝室→回 A。",
            "确定");
    }

    [MenuItem(FixCollegeACameraMenu)]
    static void FixLingCollegeACameraDefaults()
    {
        const string scenePath = "Assets/scene/lingcollegeA.unity";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
        {
            EditorUtility.DisplayDialog("寝室传送", "未找到 lingcollegeA 场景。", "确定");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        int fixedSwitchers = 0;
        int fixedCameras = 0;
        foreach (var sw in UnityEngine.Object.FindObjectsOfType<CameraSwitcher>(true))
        {
            if (sw == null)
                continue;
            if (sw.allowThirdPersonMouseControl)
            {
                Undo.RecordObject(sw, "Fix lingcollegeA camera");
                sw.allowThirdPersonMouseControl = false;
                fixedSwitchers++;
            }
            if (sw.thirdPersonCamera != null && sw.thirdPersonCamera.enabled)
            {
                Undo.RecordObject(sw.thirdPersonCamera, "Fix lingcollegeA camera");
                sw.thirdPersonCamera.enabled = false;
                fixedCameras++;
            }
        }

        if (fixedSwitchers > 0 || fixedCameras > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        EditorUtility.DisplayDialog(
            "寝室传送",
            $"已处理 lingcollegeA：CameraSwitcher={fixedSwitchers}，关闭第三人称相机={fixedCameras}。\n请保存场景后重新测试从 A 进出寝室。",
            "确定");
    }

    [MenuItem(AddAnchorsMenu)]
    static void AddReturnAnchorsToDormPortals()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("寝室传送", "请先打开要配置的场景。", "确定");
            return;
        }

        int added = 0;
        int roleSet = 0;
        foreach (var transfer in UnityEngine.Object.FindObjectsOfType<TeleportButton>(true))
        {
            if (transfer == null || string.IsNullOrEmpty(transfer.targetSceneName))
                continue;
            string key = transfer.targetSceneName.Replace("\\", "/");
            bool toDorm = key.Contains("dorm", StringComparison.OrdinalIgnoreCase);
            if (!toDorm)
                continue;

            if (transfer.dormRole == DormTeleportRole.None)
            {
                Undo.RecordObject(transfer, "Set Dorm Enter Role");
                transfer.dormRole = DormTeleportRole.EnterDorm;
                roleSet++;
            }

            if (transfer.GetComponent<DormReturnAnchor>() == null)
            {
                Undo.AddComponent<DormReturnAnchor>(transfer.gameObject);
                added++;
            }
        }

        if (added > 0 || roleSet > 0)
            EditorSceneManager.MarkSceneDirty(scene);

        EditorUtility.DisplayDialog(
            "寝室传送",
            added > 0 || roleSet > 0
                ? $"已为 {roleSet} 个传送门设置 dormRole=EnterDorm，并为 {added} 个添加 DormReturnAnchor。\n离开寝室后将回到该门的位置。"
                : "未找到目标为 dorm 的 TeleportButton，或已全部配置完成。",
            "确定");
    }

    [MenuItem(CreateExitMenu)]
    static void CreateDormExitButton()
    {
        const string dormPath = "Assets/scene/dorm.scene";
        if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(dormPath))
        {
            EditorUtility.DisplayDialog("寝室传送", "未找到 Assets/scene/dorm.scene。", "确定");
            return;
        }

        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("寝室传送", "请先退出 Play Mode。", "确定");
            return;
        }

        var scene = EditorSceneManager.OpenScene(dormPath, OpenSceneMode.Single);
        if (!scene.IsValid())
            return;

        foreach (var transfer in UnityEngine.Object.FindObjectsOfType<TeleportButton>(true))
        {
            if (transfer != null && transfer.dormRole == DormTeleportRole.ExitDorm)
            {
                Selection.activeGameObject = transfer.gameObject;
                EditorUtility.DisplayDialog("寝室传送", "dorm 场景中已存在离开寝室按钮（TeleportButton.ExitDorm）。", "确定");
                return;
            }
        }

#pragma warning disable 618
        var legacyExit = UnityEngine.Object.FindObjectOfType<DormExitTeleportButton>(true);
#pragma warning restore 618
        if (legacyExit != null)
        {
            Selection.activeGameObject = legacyExit.gameObject;
            EditorUtility.DisplayDialog(
                "寝室传送",
                "dorm 场景中仍有旧版 DormExitTeleportButton。\n请在该物体上添加 TeleportButton 并设 dormRole=ExitDorm，或删除旧组件后重新运行本菜单。",
                "确定");
            return;
        }

        var canvas = UnityEngine.Object.FindObjectOfType<Canvas>(true);
        if (canvas == null)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
        }

        var btnGo = new GameObject("Button_LeaveDorm", typeof(RectTransform), typeof(Image), typeof(Button));
        Undo.RegisterCreatedObjectUndo(btnGo, "Create Leave Dorm Button");
        btnGo.transform.SetParent(canvas.transform, false);

        var rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-24f, -24f);
        rt.sizeDelta = new Vector2(160f, 48f);

        var img = btnGo.GetComponent<Image>();
        img.color = new Color(0.15f, 0.45f, 0.85f, 0.92f);

        var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        Undo.RegisterCreatedObjectUndo(textGo, "Create Leave Dorm Text");
        textGo.transform.SetParent(btnGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        text.text = "离开寝室";
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var teleport = Undo.AddComponent<TeleportButton>(btnGo);
        teleport.dormRole = DormTeleportRole.ExitDorm;
        teleport.dormExitFallbackSceneName = DormSceneConfig.DefaultFallbackReturnScene;
        teleport.dormExitFallbackPosition = DormSceneConfig.DefaultFallbackReturnPosition;
        teleport.dormExitFallbackRotation = DormSceneConfig.DefaultFallbackReturnRotation;

        Selection.activeGameObject = btnGo;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog(
            "寝室传送",
            "已在 dorm 场景右上角创建「离开寝室」按钮（TeleportButton，dormRole=ExitDorm）。\n请保存场景后测试。",
            "确定");
    }

    [MenuItem(AddExitToOpenMenu)]
    static void ConfigureExitOnSelection()
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("寝室传送", "请先选中一个带 Button 的 UI 物体。", "确定");
            return;
        }

        if (go.GetComponent<Button>() == null)
        {
            EditorUtility.DisplayDialog("寝室传送", "选中物体需要带有 Button 组件。", "确定");
            return;
        }

        var teleport = go.GetComponent<TeleportButton>();
        if (teleport == null)
            teleport = Undo.AddComponent<TeleportButton>(go);

        Undo.RecordObject(teleport, "Configure Dorm Exit");
        teleport.dormRole = DormTeleportRole.ExitDorm;
        if (string.IsNullOrWhiteSpace(teleport.dormExitFallbackSceneName))
            teleport.dormExitFallbackSceneName = DormSceneConfig.DefaultFallbackReturnScene;

        EditorUtility.DisplayDialog(
            "寝室传送",
            "已配置 TeleportButton（dormRole=ExitDorm）。\n可在 Inspector 调整兜底返回场景与坐标。",
            "确定");
    }
}
#endif

