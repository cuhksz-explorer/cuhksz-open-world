using TMPro;
using UnityEngine;

/// <summary>
/// 场景中一面「旗帜」：绑定唯一 <see cref="slotId"/>（勿含英文逗号），文字与颜色由服务器同步给全服客户端。
/// </summary>
[DisallowMultipleComponent]
public class FlagAuctionSlotView : MonoBehaviour
{
    [Tooltip("全局唯一 ID，须与服务器及竞拍面板一致；勿使用英文逗号。")]
    [SerializeField] string slotId = "flag_01";

    [SerializeField] TextMeshPro textMeshPro;
    [SerializeField] Renderer[] colorTargets;

    MaterialPropertyBlock _mpb;

    public string SlotId => (slotId ?? "").Trim();

    void Reset()
    {
        if (textMeshPro == null)
            textMeshPro = GetComponentInChildren<TextMeshPro>(true);
    }

    void OnEnable()
    {
        FlagAuctionStatePoller.EnsureExists();
        if (FlagAuctionStatePoller.Instance != null)
            FlagAuctionStatePoller.Instance.Register(this);
    }

    void OnDisable()
    {
        if (FlagAuctionStatePoller.Instance != null)
            FlagAuctionStatePoller.Instance.Unregister(this);
    }

    public void ApplyWire(FlagAuctionSlotWire w)
    {
        if (w == null)
        {
            if (textMeshPro != null)
                textMeshPro.text = "";
            return;
        }

        if (textMeshPro != null)
        {
            textMeshPro.text = w.display_text ?? "";
            textMeshPro.color = new Color(w.color_r, w.color_g, w.color_b, w.color_a);
        }

        if (colorTargets != null && colorTargets.Length > 0)
        {
            var c = new Color(w.color_r, w.color_g, w.color_b, w.color_a);
            if (_mpb == null)
                _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < colorTargets.Length; i++)
            {
                if (colorTargets[i] == null) continue;
                colorTargets[i].GetPropertyBlock(_mpb);
                if (colorTargets[i].sharedMaterial != null &&
                    colorTargets[i].sharedMaterial.HasProperty("_Color"))
                    _mpb.SetColor("_Color", c);
                colorTargets[i].SetPropertyBlock(_mpb);
            }
        }
    }
}
