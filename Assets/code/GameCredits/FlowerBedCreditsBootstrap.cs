using System;
using GameQuestSystem;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// undergarden 进入时把致谢触发点对齐到「大花篮」，并确保挂载 <see cref="FlowerBedCreditsTrigger"/>。
/// </summary>
public static class FlowerBedCreditsBootstrap
{
    const string SceneName = "undergarden";
    const string TriggerName = "FlowerBed_CreditsTrigger";
    const string FlowerBasketName = "大花篮";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnAfterSceneLoad()
    {
        TrySetup(SceneManager.GetActiveScene());
    }

    public static void TrySetup(Scene scene)
    {
        if (!scene.IsValid()
            || !string.Equals(scene.name, SceneName, StringComparison.OrdinalIgnoreCase))
            return;

        var basket = FindInScene(scene, FlowerBasketName);
        if (basket == null)
            return;

        var triggerGo = GameObject.Find(TriggerName);
        if (triggerGo == null)
            triggerGo = new GameObject(TriggerName);

        triggerGo.transform.SetPositionAndRotation(
            basket.transform.position,
            Quaternion.identity);

        var wrongNpc = triggerGo.GetComponent<StoryQuestNpc>();
        if (wrongNpc != null)
            UnityEngine.Object.Destroy(wrongNpc);

        if (triggerGo.GetComponent<FlowerBedCreditsTrigger>() == null)
            triggerGo.AddComponent<FlowerBedCreditsTrigger>();
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
