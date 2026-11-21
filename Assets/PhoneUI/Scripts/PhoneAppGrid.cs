using UnityEngine;

namespace PhoneUI
{
    public class PhoneAppGrid : MonoBehaviour
    {
        public PhoneAppSlot[] slots;

        void OnDisable()
        {
            ResetAllSlots();
        }

        public void ResetAllSlots()
        {
            if (slots == null) return;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null) continue;
                var v = slots[i].visual;
                if (v != null)
                    v.ResetToIdle();
            }
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (slots == null || slots.Length == 0)
                slots = GetComponentsInChildren<PhoneAppSlot>(true);
        }
#endif
    }
}
