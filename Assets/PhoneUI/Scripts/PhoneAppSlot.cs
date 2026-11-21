using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PhoneUI
{
  public class PhoneAppSlot : MonoBehaviour
  {
    [Header("App")]
    public PhoneAppId appId = PhoneAppId.None;
    public string displayName = "未绑定";
    public string futureBindingId;

    [Header("UI")]
    public TextMeshProUGUI labelText;
    public TextMeshProUGUI hotkeyHintText;
    public Image iconImage;
    public PhoneAppSlotVisual visual;
    public Button hitAreaButton;

    [Header("Routing")]
    public PhoneSessionController session;

    bool _clickWired;

    void Awake()
    {
      if (visual == null)
        visual = GetComponent<PhoneAppSlotVisual>();
      if (session == null)
        session = GetComponentInParent<PhoneSessionController>();
      HideFaceLabel();
      PhoneUiAssetLoader.ApplyTmpFont(labelText);
      PhoneUiAssetLoader.ApplyTmpFont(hotkeyHintText);
      if (hotkeyHintText != null && ShouldApplyDefaultHotkey(hotkeyHintText.text))
        hotkeyHintText.text = "--";
    }

        void Start()
        {
            ApplySlotIcon();
            WireClick();
        }

        void OnEnable()
        {
            ApplySlotIcon();
            WireClick();
        }

        /// <summary>将 CellPhone/UI 图标应用到 SlotFace（预制体 iconImage 常为空）。</summary>
        public void ApplySlotIcon()
        {
            Sprite sprite = appId == PhoneAppId.Custom2
                ? PhoneUiSimpleIcons.GetLogoutIcon()
                : PhoneUiAssetLoader.GetAppIconSprite(appId);
            if (sprite == null)
                return;

            Image target = iconImage;
            if (target == null && visual != null)
                target = visual.slotFace;
            if (target == null)
                return;

            target.sprite = sprite;
            target.color = Color.white;
            target.preserveAspect = true;
        }

    /// <summary>仅保留格位下方标题，隐藏 SlotFace 内重复标签。</summary>
    public void ApplyBottomCaption(string label)
    {
      if (!string.IsNullOrEmpty(label))
        displayName = label;

      HideFaceLabel();

      var caption = hotkeyHintText;
      if (caption == null)
      {
        var existing = transform.Find("CaptionBelow");
        if (existing != null)
          caption = existing.GetComponent<TextMeshProUGUI>();
      }

      if (caption == null)
        caption = CreateBottomCaption();

      hotkeyHintText = caption;

      var slotRt = transform as RectTransform;
      var rt = caption.rectTransform;
      rt.SetParent(slotRt, false);
      rt.SetAsLastSibling();
      rt.anchorMin = new Vector2(0f, 0f);
      rt.anchorMax = new Vector2(1f, 0f);
      rt.pivot = new Vector2(0.5f, 1f);
      rt.anchoredPosition = Vector2.zero;
      rt.sizeDelta = new Vector2(0f, 22f);

      caption.text = label;
      caption.fontSize = 13f;
      caption.alignment = TextAlignmentOptions.Center;
      caption.color = PhoneUiTheme.TextBlack;
      caption.enableWordWrapping = false;
      caption.overflowMode = TextOverflowModes.Overflow;
      caption.raycastTarget = false;
      PhoneUiAssetLoader.ApplyTmpFont(caption);
      caption.gameObject.SetActive(true);
    }

    void HideFaceLabel()
    {
      if (labelText == null)
        return;

      labelText.text = string.Empty;
      labelText.gameObject.SetActive(false);
    }

    TextMeshProUGUI CreateBottomCaption()
    {
      var go = new GameObject("CaptionBelow", typeof(RectTransform));
      go.transform.SetParent(transform, false);
      return go.AddComponent<TextMeshProUGUI>();
    }

    public void WireClick()
    {
      if (_clickWired || hitAreaButton == null)
        return;
      if (session == null)
        session = GetComponentInParent<PhoneSessionController>();
      hitAreaButton.transition = Selectable.Transition.None;
      hitAreaButton.onClick.RemoveListener(OnSlotClicked);
      hitAreaButton.onClick.AddListener(OnSlotClicked);
      _clickWired = true;
    }

    static bool ShouldApplyDefaultHotkey(string text)
    {
      if (string.IsNullOrWhiteSpace(text))
        return true;
      return text == "New Text";
    }

    public void OnSlotClicked()
    {
      if (session == null)
      {
        Debug.LogWarning("[PhoneAppSlot] session 未绑定: " + name);
        return;
      }
      session.HandleAppSlotClick(this);
    }
  }
}
