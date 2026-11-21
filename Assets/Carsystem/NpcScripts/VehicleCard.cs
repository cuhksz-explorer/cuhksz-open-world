using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 挂在车辆卡片预制体上，负责显示缩略图、名称，并将点击事件传递给 VehicleShopUI
/// </summary>
public class VehicleCard : MonoBehaviour
{
    static readonly Color NormalBg = Color.white;
    static readonly Color SelectedBg = new Color(1f, 0.96f, 0.88f, 1f);
    static readonly Color GoldBorder = new Color(0.79f, 0.66f, 0.28f, 1f);
    static readonly Color NormalBorder = new Color(0.88f, 0.88f, 0.88f, 1f);

    [Header("UI 组件（拖拽绑定）")]
    public Image thumbnailImage;
    public TMP_Text nameText;
    public Image cardBackground;
    public Image selectionBorder;

    VehicleData vehicleData;
    VehicleShopUI shopUI;
    bool isSelected;

    public VehicleData VehicleData => vehicleData;

    void Awake()
    {
        if (cardBackground == null)
            cardBackground = GetComponent<Image>();
        EnsureSelectionBorder();
    }

    void EnsureSelectionBorder()
    {
        if (selectionBorder != null)
            return;

        Transform existing = transform.Find("SelectionBorder");
        if (existing != null)
        {
            selectionBorder = existing.GetComponent<Image>();
            return;
        }

        var borderGo = new GameObject("SelectionBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        borderGo.transform.SetParent(transform, false);
        borderGo.transform.SetAsFirstSibling();

        var rt = borderGo.GetComponent<RectTransform>();
        StretchFill(rt);

        selectionBorder = borderGo.GetComponent<Image>();
        selectionBorder.color = NormalBorder;
        selectionBorder.raycastTarget = false;
        selectionBorder.sprite = cardBackground != null ? cardBackground.sprite : null;
        selectionBorder.type = Image.Type.Sliced;
    }

    /// <summary>
    /// 由 VehicleShopUI 调用，初始化卡片数据
    /// </summary>
    public void Setup(VehicleData data, VehicleShopUI ui)
    {
        vehicleData = data;
        shopUI = ui;

        if (thumbnailImage != null)
            thumbnailImage.sprite = data.thumbnail;
        if (nameText != null)
        {
            nameText.text = data.vehicleName;
            nameText.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            VehicleShopTypography.Apply(nameText);
            nameText.enableWordWrapping = false;
            nameText.overflowMode = TextOverflowModes.Ellipsis;
            nameText.alignment = TextAlignmentOptions.MidlineLeft;
        }

        EnsureSelectionBorder();
        ApplyCardLayout();
        SetSelected(false);

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() => shopUI.OnVehicleSelected(vehicleData));
        }
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (cardBackground != null)
            cardBackground.color = selected ? SelectedBg : NormalBg;

        if (selectionBorder != null)
        {
            selectionBorder.enabled = true;
            selectionBorder.color = selected ? GoldBorder : NormalBorder;
        }
    }

    void ApplyCardLayout()
    {
        if (thumbnailImage != null)
        {
            var rt = thumbnailImage.rectTransform;
            rt.SetSiblingIndex(selectionBorder != null ? 1 : 0);
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(10f, 0f);
            rt.sizeDelta = new Vector2(72f, 72f);
            thumbnailImage.preserveAspect = true;
        }

        if (nameText != null)
        {
            var rt = nameText.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(90f, 8f);
            rt.offsetMax = new Vector2(-10f, -8f);
            nameText.transform.SetAsLastSibling();
        }
    }

    static void StretchFill(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
