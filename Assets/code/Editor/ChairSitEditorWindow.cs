#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 椅子坐下配置窗口（菜单搜不到时用 Window 打开）。
/// </summary>
public class ChairSitEditorWindow : EditorWindow
{
    [MenuItem("Tools/Chair Sit/Open Setup Window", false, 0)]
    [MenuItem("Window/Chair Sit Setup", false, 2050)]
    static void OpenWindow()
    {
        var w = GetWindow<ChairSitEditorWindow>(false, "椅子坐下配置", true);
        w.minSize = new Vector2(360, 240);
        w.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("椅子坐下 · 零基础配置", EditorStyles.boldLabel);
        GUILayout.Space(6);
        GUILayout.Label("步骤：\n1. 打开 undergarden 1 场景（标题栏会显示）\n2. 确保不在 Play 模式（顶部播放键不是蓝的）\n3. 点下面绿色大按钮\n4. Ctrl+S 保存", EditorStyles.wordWrappedLabel);
        GUILayout.Space(8);

        bool isPlaying = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying;
        if (isPlaying)
        {
            EditorGUILayout.HelpBox(
                "现在处于 Play 模式，按钮已禁用。请先点 Unity 顶部蓝色播放键停止 Play。",
                MessageType.Warning);
        }

        using (new EditorGUI.DisabledScope(isPlaying))
        {
            GUI.backgroundColor = new Color(0.55f, 0.85f, 0.65f);
            if (GUILayout.Button("一键：当前场景配置（推荐 · 只改 undergarden 1）", GUILayout.Height(44)))
            {
                ChairSitOneClickWizard.RunFullSetupWithBenchesPublic();
            }
            GUI.backgroundColor = Color.white;
            GUILayout.Space(6);

            if (GUILayout.Button("仅配置动画（玩家坐下动作）", GUILayout.Height(28)))
            {
                ChairSitOneClickWizard.RunFullSetupPublic();
            }

            if (GUILayout.Button("仅给当前场景所有 bench 加坐下交互", GUILayout.Height(28)))
            {
                InteractableChairSetup.AddChairInteractionAllBenches();
            }

            GUILayout.Space(8);
            if (GUILayout.Button("给当前选中的物体加坐下", GUILayout.Height(28)))
            {
                InteractableChairSetup.AddChairInteractionSelectionPublic();
            }

            GUILayout.Space(8);
            GUI.backgroundColor = new Color(0.55f, 0.85f, 0.65f);
            if (GUILayout.Button("演示：传送到长椅旁", GUILayout.Height(32)))
            {
                ChairSitDemoTools.TeleportPlayerBesideChairAndSetup();
            }
            GUI.backgroundColor = Color.white;

            GUILayout.Space(8);
            EditorGUILayout.LabelField("高级", EditorStyles.miniBoldLabel);
            if (GUILayout.Button("跨场景批量（会修改其他场景，慎用）", GUILayout.Height(24)))
            {
                ChairSitAllScenesBatch.RunAllScenes();
            }
        }

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "若按完按钮还没效果：\n" +
            "· Hierarchy 搜 bench，看是否有 InteractableChair\n" +
            "· 玩家上是否有 Player Sit Sequence\n" +
            "· Console 有无红色报错",
            MessageType.Info);
    }

    void OnEnable()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    void OnDisable()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    void OnPlayModeChanged(PlayModeStateChange _)
    {
        Repaint();
    }
}
#endif
