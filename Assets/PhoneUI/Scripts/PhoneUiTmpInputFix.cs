using TMPro;
using UnityEngine;

namespace PhoneUI
{
    /// <summary>修复 TMP_InputField 缺少 textViewport 时在拖拽选字阶段抛 NullReference 的问题。</summary>
    public static class PhoneUiTmpInputFix
    {
        public static void FixUnder(GameObject root)
        {
            if (root == null)
                return;

            var fields = root.GetComponentsInChildren<TMP_InputField>(true);
            for (int i = 0; i < fields.Length; i++)
                FixField(fields[i]);
        }

        public static void FixField(TMP_InputField field)
        {
            if (field == null || field.textViewport != null)
                return;

            if (field.textComponent != null)
            {
                var parentRt = field.textComponent.transform.parent as RectTransform;
                if (parentRt != null)
                {
                    field.textViewport = parentRt;
                    return;
                }

                field.textViewport = field.textComponent.rectTransform;
            }
        }
    }
}
