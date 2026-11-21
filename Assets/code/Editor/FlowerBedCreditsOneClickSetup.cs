#if UNITY_EDITOR
using GameQuestSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 undergarden 大花篮附近放置致谢触发区（按 F 查看制作人员名单）。
/// </summary>
public static class FlowerBedCreditsOneClickSetup
{
    const string MenuPath = "Tools/Game Credits/在 undergarden 大花篮放置致谢触发区";
    const string TriggerName = "FlowerBed_CreditsTrigger";
    const string FlowerBasketName = "大花篮";

    static readonly Vector3 FallbackFlowerBedCenter = new Vector3(-1359.56006f, 14.5799999f, 1075.28003f);

    [MenuItem(MenuPath)]
    public static void SetupInUndergarden()
    {
        var scene = EditorSceneManager.GetSceneByName("undergarden");
        if (!scene.IsValid() || !scene.isLoaded)
        {
            if (!EditorUtility.DisplayDialog(
                    "大花篮致谢",
                    "当前未打开 undergarden 场景。\n是否现在打开并放置触发区？",
                    "打开并放置",
                    "取消"))
                return;

            scene = EditorSceneManager.OpenScene("Assets/scene/undergarden.unity", OpenSceneMode.Single);
        }

        var basket = FindInScene(scene, FlowerBasketName);
        Vector3 position = basket != null ? basket.transform.position : FallbackFlowerBedCenter;

        var triggerGo = GameObject.Find(TriggerName);
        bool created = triggerGo == null;
        if (triggerGo == null)
            triggerGo = new GameObject(TriggerName);

        Undo.RegisterFullObjectHierarchyUndo(triggerGo, "Flower Bed Credits Trigger");
        triggerGo.transform.position = position;

        var wrongNpc = triggerGo.GetComponent<StoryQuestNpc>();
        if (wrongNpc != null)
            Undo.DestroyObjectImmediate(wrongNpc);

        if (triggerGo.GetComponent<FlowerBedCreditsTrigger>() == null)
            Undo.AddComponent<FlowerBedCreditsTrigger>(triggerGo);

        if (created)
            Undo.RegisterCreatedObjectUndo(triggerGo, "Create Flower Bed Credits Trigger");

        Selection.activeGameObject = triggerGo;
        EditorSceneManager.MarkSceneDirty(scene);

        string anchor = basket != null
            ? $"已对齐到大花篮：{position}"
            : $"未找到「{FlowerBasketName}」，使用备用坐标：{position}";

        EditorUtility.DisplayDialog(
            "大花篮致谢",
            anchor + "\n\n" +
            "· 走近 4.5m 内显示交互提示\n" +
            "· 按 F 打开制作组名单（可滚动）\n" +
            "· Esc 关闭\n\n" +
            "请保存场景后进入 Play 测试。",
            "确定");
    }

    static GameObject FindInScene(Scene scene, string objectName)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var found = FindDeep(roots[i].transform, objectName);
            if (found != null)
                return found.gameObject;
        }

        return null;
    }

    static Transform FindDeep(Transform root, string objectName)
    {
        if (root.name == objectName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindDeep(root.GetChild(i), objectName);
            if (found != null)
                return found;
        }

        return null;
    }
}
#endif
