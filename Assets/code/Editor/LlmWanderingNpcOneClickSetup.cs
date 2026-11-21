#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

/// <summary>
/// 选中角色模型后一键配置：NavMesh 游走 + F 键千问对话 + 支线上报。
/// </summary>
public static class LlmWanderingNpcOneClickSetup
{
    const string ConfigureMenu = "Tools/Quest System/一键配置选中角色为 LLM 游走 NPC";
    const string PrefabMenu = "Tools/Quest System/生成 LLM 对话面板预制体";
    const string PrefabPath = "Assets/Prefabs/LLM/LlmNpcDialoguePanel.prefab";

    const float InteractDistance = 4f;
    const float WalkRadius = 12f;

    [MenuItem(ConfigureMenu, true)]
    static bool ValidateConfigure()
    {
        return Selection.activeGameObject != null;
    }

    [MenuItem(ConfigureMenu, false, 45)]
    static void ConfigureSelected()
    {
        var root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("LLM 游走 NPC", "请先在 Hierarchy 选中人物模型根节点。", "确定");
            return;
        }

        Undo.SetCurrentGroupName("Configure LLM Wandering NPC");
        int undoGroup = Undo.GetCurrentGroup();

        EnsureCapsuleCollider(root);
        EnsureNavMeshWalker(root);
        Transform anchor = EnsureInteractAnchor(root);

        var built = EnsureDialogueUi(root);
        var interaction = EnsureInteractionManager(root, anchor, built);
        WireEmotionAndQuest(root, built);

        var settings = LlmNpcApiSettings.LoadOrCreateDefault();
        if (settings != null && built.chatClient != null)
            built.chatClient.ApplyApiSettings(settings);

        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(root.scene);

        Undo.CollapseUndoOperations(undoGroup);

        string navWarning = SceneHasNavMeshData()
            ? ""
            : "\n\n注意：当前场景可能未烘焙 NavMesh，游走不会生效。请 Window → AI → Navigation 烘焙后再试。";

        string apiWarning = settings != null && string.IsNullOrWhiteSpace(settings.apiKey)
            ? "\n\n请运行 Tools/Quest System/配置千问 API（LLM NPC）填写 apiKey。"
            : "";

        EditorUtility.DisplayDialog(
            "LLM 游走 NPC",
            "已为「" + root.name + "」完成配置：\n" +
            "· NavMesh 随机游走\n" +
            "· 靠近显示提示，按 F 打开千问对话\n" +
            "· 支线对话上报（LlmNpcQuestReporter）" +
            navWarning + apiWarning,
            "确定");

        Selection.activeGameObject = root;
    }

    [MenuItem(PrefabMenu, false, 46)]
    static void GenerateDialoguePrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/LLM"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "LLM");

        var temp = new GameObject("_LlmNpcDialoguePrefabTemp");
        try
        {
            var built = LlmNpcDialogueUiFactory.BuildUnder(temp.transform, "NPC");
            var prefab = PrefabUtility.SaveAsPrefabAsset(built.root, PrefabPath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[LLM NPC] 已保存预制体: " + PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(temp);
        }
    }

    static void EnsureCapsuleCollider(GameObject root)
    {
        var col = root.GetComponent<CapsuleCollider>();
        if (col == null)
            col = Undo.AddComponent<CapsuleCollider>(root);

        col.isTrigger = false;
        if (col.height < 1.5f)
        {
            col.height = 1.8f;
            col.radius = 0.35f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    static void EnsureNavMeshWalker(GameObject root)
    {
        var agent = root.GetComponent<NavMeshAgent>();
        if (agent == null)
            agent = Undo.AddComponent<NavMeshAgent>(root);

        agent.speed = 1.2f;
        agent.angularSpeed = 360f;
        agent.acceleration = 4f;
        agent.stoppingDistance = 0.35f;

        var walker = root.GetComponent<RandomWalkNPC>();
        if (walker == null)
            walker = Undo.AddComponent<RandomWalkNPC>(root);
        walker.walkRadius = WalkRadius;
    }

    static Transform EnsureInteractAnchor(GameObject root)
    {
        var existing = root.transform.Find("InteractAnchor");
        if (existing != null)
            return existing;

        var go = new GameObject("InteractAnchor");
        Undo.RegisterCreatedObjectUndo(go, "Create InteractAnchor");
        go.transform.SetParent(root.transform, false);
        float y = 1.45f;
        var col = root.GetComponent<CapsuleCollider>();
        if (col != null)
            y = col.center.y;
        go.transform.localPosition = new Vector3(0f, y, 0f);
        return go.transform;
    }

    static LlmNpcDialogueUiFactory.BuiltUi EnsureDialogueUi(GameObject root)
    {
        var existingCanvas = root.transform.Find("LlmNpcDialogueCanvas");
        if (existingCanvas != null)
        {
            var chat = existingCanvas.GetComponent<LLMChatClient_LocalQwen>();
            if (chat == null)
                chat = Undo.AddComponent<LLMChatClient_LocalQwen>(existingCanvas.gameObject);

            return new LlmNpcDialogueUiFactory.BuiltUi
            {
                root = existingCanvas.gameObject,
                canvas = existingCanvas.GetComponent<Canvas>(),
                dialoguePanel = existingCanvas.Find("LlmDialogueBottomPanel")?.gameObject,
                chatClient = chat,
                closeButton = existingCanvas.GetComponentInChildren<Button>(true),
                inputField = chat.inputField,
                resultText = chat.resultText
            };
        }

        var built = LlmNpcDialogueUiFactory.BuildUnder(root.transform, root.name);
        Undo.RegisterCreatedObjectUndo(built.root, "Create LLM Dialogue UI");
        return built;
    }

    static CanvasInteractionUIManager EnsureInteractionManager(
        GameObject root,
        Transform anchor,
        LlmNpcDialogueUiFactory.BuiltUi built)
    {
        var mgr = root.GetComponent<CanvasInteractionUIManager>();
        if (mgr == null)
            mgr = Undo.AddComponent<CanvasInteractionUIManager>(root);

        mgr.proximityTargets = new[]
        {
            new InteractProximityTarget
            {
                trigger = anchor,
                displayName = root.name,
                interactionType = "对话",
                dialogueNpcRoot = root.transform
            }
        };

        mgr.iconTriggerDistance = InteractDistance;
        mgr.distanceBuffer = 0.5f;
        mgr.activateKey = KeyCode.F;
        mgr.showKeyHintOnTypeLine = true;
        mgr.autoBindSharedInteractPromptFromScene = true;
        mgr.defaultDisplayName = root.name;
        mgr.defaultInteractionType = "对话";

        mgr.dialoguePanel = built.root;
        mgr.dialogueTargetCanvas = built.canvas;
        mgr.chatClient = built.chatClient;
        mgr.closeDialogueButton = built.closeButton;

        var pm = Object.FindObjectOfType<PlayerMovement>(true);
        if (pm != null)
        {
            mgr.playerMovement = pm;
            mgr.playerModelTransform = pm.transform;
        }

        if (built.chatClient != null)
        {
            built.chatClient.npcWalker = root.GetComponent<RandomWalkNPC>();
            var emo = root.GetComponent<NPCEmotionController>();
            if (emo != null)
                built.chatClient.npcEmotion = emo;
        }

        if (built.closeButton != null)
        {
            built.closeButton.onClick.RemoveAllListeners();
            built.closeButton.onClick.AddListener(mgr.CloseDialogueFromExternal);
        }

        return mgr;
    }

    static void WireEmotionAndQuest(GameObject root, LlmNpcDialogueUiFactory.BuiltUi built)
    {
        var animator = root.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            var emo = root.GetComponent<NPCEmotionController>();
            if (emo == null)
                emo = Undo.AddComponent<NPCEmotionController>(root);
            emo.animator = animator;

            if (root.GetComponent<Qcontrol>() == null)
            {
                var qc = Undo.AddComponent<Qcontrol>(root);
                qc.animator = animator;
            }
        }

        var reporter = root.GetComponent<LlmNpcQuestReporter>();
        if (reporter == null)
            reporter = Undo.AddComponent<LlmNpcQuestReporter>(root);
        reporter.npcId = root.name;

        if (built.chatClient != null)
            reporter.BindChatClient(built.chatClient);
    }

    static bool SceneHasNavMeshData()
    {
        var triangulation = NavMesh.CalculateTriangulation();
        return triangulation.vertices != null && triangulation.vertices.Length > 0;
    }
}
#endif
