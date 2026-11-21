#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 一键 UI 配置后的收尾：重绑设置按钮等。
/// </summary>
public static class UndergardenUiPostConfigure
{
    public static int RewireSettingsButtons(Scene scene)
    {
        if (!scene.IsValid())
            return 0;

        var phoneMgr = FindInScene<PhoneUIManager>(scene);
        int wired = 0;

        foreach (var btn in FindAllInScene<Button>(scene))
        {
            if (btn == null)
                continue;

            string name = btn.name ?? "";
            if (!name.StartsWith("Qualitychanger", System.StringComparison.Ordinal))
                continue;

            Undo.RecordObject(btn, "Rewire settings button");
            int persistentCount = btn.onClick.GetPersistentEventCount();
            for (int i = persistentCount - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);

            if (phoneMgr != null)
            {
                UnityEventTools.AddPersistentListener(btn.onClick, phoneMgr.OpenPhoneToSettings);
                EditorUtility.SetDirty(btn);
                wired++;
                continue;
            }

            var session = FindInScene<PhoneUI.PhoneSessionController>(scene);
            if (session != null)
            {
                UnityEventTools.AddPersistentListener(btn.onClick, session.OpenPhoneToSettings);
                EditorUtility.SetDirty(btn);
                wired++;
            }
        }

        return wired;
    }

    static T FindInScene<T>(Scene scene) where T : Component
    {
        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item != null && item.gameObject.scene == scene)
                return item;
        }

        return null;
    }

    static System.Collections.Generic.List<T> FindAllInScene<T>(Scene scene) where T : Component
    {
        var list = new System.Collections.Generic.List<T>();
        foreach (var item in Resources.FindObjectsOfTypeAll<T>())
        {
            if (item != null && item.gameObject.scene == scene)
                list.Add(item);
        }

        return list;
    }
}
#endif
