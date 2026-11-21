#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace PhoneUI.Editor
{
    public static class PhoneUiEditorMenus
    {
        const string SmartphoneSrc = "Assets/pictures/smartphone.png";
        const string SmartphoneDst = "Assets/PhoneUI/Resources/PhoneUI/smartphone.png";

        [MenuItem("PhoneUI/Copy Smartphone To Resources")]
        static void CopySmartphoneToResources()
        {
            if (!AssetDatabase.CopyAsset(SmartphoneSrc, SmartphoneDst))
            {
                Debug.LogWarning("[PhoneUI] 复制失败，请确认存在: " + SmartphoneSrc);
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log("[PhoneUI] 已复制到 " + SmartphoneDst);
        }

        [MenuItem("PhoneUI/Select Phone In Active Scene")]
        static void SelectPhone()
        {
            var launcher = GameObject.Find("PhoneLauncher");
            if (launcher != null)
            {
                Selection.activeGameObject = launcher;
                return;
            }

            var phone = GameObject.Find("Phone");
            if (phone != null)
                Selection.activeGameObject = phone;
            else
                Debug.LogWarning("[PhoneUI] 当前场景未找到 PhoneLauncher 或 Phone。");
        }
    }
}
#endif
