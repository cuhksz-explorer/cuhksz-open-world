#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 演示用：把玩家放到长椅旁、一键进入 Play 测试。
/// </summary>
public static class ChairSitDemoTools
{
    [MenuItem("Tools/Chair Sit/演示：传送到长椅旁并配置玩家", false, 50)]
    [MenuItem("Tools/椅子交互/演示：传送到长椅旁并配置玩家", false, 50)]
    public static void TeleportPlayerBesideChairAndSetup()
    {
        if (!ChairSitEditorGuard.EnsureNotPlaying())
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ChairSitOneClickWizard.RunFullSetupPublic();

        var chairs = Object.FindObjectsOfType<InteractableChair>(true);
        if (chairs.Length == 0)
        {
            InteractableChairSetup.AddChairInteractionAllBenches();
            chairs = Object.FindObjectsOfType<InteractableChair>(true);
        }

        if (chairs.Length == 0)
        {
            EditorUtility.DisplayDialog("椅子演示", "场景里仍没有椅子交互组件。\n请先打开 undergarden 1 场景。", "确定");
            return;
        }

        InteractableChair target = null;
        float best = float.MaxValue;
        var sceneCenter = Vector3.zero;
        int loaded = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded)
                continue;
            loaded++;
            var roots = s.GetRootGameObjects();
            if (roots.Length > 0)
                sceneCenter += roots[0].transform.position;
        }
        if (loaded > 0)
            sceneCenter /= loaded;

        foreach (var c in chairs)
        {
            if (c == null || c.sitPoint == null)
                continue;
            float d = Vector3.Distance(c.sitPoint.position, sceneCenter);
            if (d < best)
            {
                best = d;
                target = c;
            }
        }
        target ??= chairs[0];

        var player = Object.FindObjectOfType<PlayerMovement>(true);
        if (player == null)
        {
            EditorUtility.DisplayDialog("椅子演示", "未找到玩家（PlayerMovement）。", "确定");
            return;
        }

        Transform sit = target.sitPoint;
        Vector3 forward = sit.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f)
            forward = target.transform.forward;
        forward.Normalize();

        Vector3 standPos = sit.position - forward * 1.4f;
        standPos.y = sit.position.y;

        Undo.RecordObject(player.transform, "Teleport To Chair");
        player.transform.SetPositionAndRotation(standPos, Quaternion.LookRotation(forward, Vector3.up));

        var sitSeq = player.GetComponent<PlayerSitSequence>();
        if (sitSeq == null)
            sitSeq = Undo.AddComponent<PlayerSitSequence>(player.gameObject);

        sitSeq.sitDownClip = FindClipByCatalog(ChairSitClipCatalog.SitDownCandidates);
        sitSeq.sitIdleClip = FindClipByCatalog(ChairSitClipCatalog.SitIdleCandidates);
        sitSeq.standUpClip = FindClipByCatalog(ChairSitClipCatalog.StandUpCandidates);
        sitSeq.sitAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/Animations/ChairSit/PlayerChairSit.controller");
        EditorUtility.SetDirty(sitSeq);
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

        Selection.activeGameObject = player.gameObject;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorUtility.DisplayDialog(
            "椅子演示",
            $"已把玩家放到「{target.displayName}」旁。\n\n" +
            "请点 Play ▶，然后：\n" +
            "1. 看系统统一交互提示\n" +
            "2. 按 [F] 坐下\n" +
            "3. 坐下后再按 [F] 站起\n\n" +
            "最后 Ctrl+S 保存场景。",
            "好的");

        Debug.Log($"[椅子演示] 玩家已传到 {target.name} 旁，距离坐姿点约 1.4m。");
    }

    [MenuItem("Tools/Chair Sit/演示：直接 Play 测试", false, 51)]
    [MenuItem("Tools/椅子交互/演示：直接 Play 测试", false, 51)]
    public static void PlayDemo()
    {
        TeleportPlayerBesideChairAndSetup();
        if (!Application.isPlaying)
            EditorApplication.isPlaying = true;
    }

    static AnimationClip FindClipByCatalog(string[] candidates)
    {
        string[] folders = { "Assets/Animations/ChairSit", "Assets/Resources/ChairSit" };
        for (int f = 0; f < folders.Length; f++)
        {
            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folders[f] });
            var clips = new AnimationClip[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[i]));
            var clip = ChairSitClipCatalog.PickClip(clips, candidates);
            if (clip != null)
                return clip;
        }

        return null;
    }
}
#endif
