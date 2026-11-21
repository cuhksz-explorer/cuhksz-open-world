using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DormMall
{
    /// <summary>单个商品卡片 UI（支持 Text 或 TextMeshProUGUI）</summary>
    public class ProductItemUI : MonoBehaviour
    {
        const string BuyLabelZh = "购买";
        const string BuyLabelEn = "Buy";

        [Header("引用")]
        public Image iconImage;
        public Text nameText;
        public Text categoryText;
        public Text priceText;
        public Button buyButton;

        private ProductEntry _entry;
        private Sprite _placeholderSprite;

        /// <summary>设置商品数据</summary>
        public void Setup(ProductEntry entry, ProductDatabase database, Sprite placeholderSprite = null)
        {
            _entry = entry;
            _placeholderSprite = placeholderSprite;

            if (nameText) nameText.text = Loc.Term(entry.productName);
            if (categoryText)
            {
                string effects = BuildFoodEffectLabel(entry);
                string category = Loc.Term(entry.category);
                categoryText.text = string.IsNullOrEmpty(effects)
                    ? category
                    : (category + " | " + effects);
            }

            int displayPrice = Mathf.RoundToInt(entry.price);
            if (priceText) priceText.text = displayPrice.ToString();

            if (iconImage)
            {
                DormMallIconLoader.SetIcon(iconImage, database, entry, placeholderSprite);
            }

            if (buyButton)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(OnBuyClicked);
                ApplyBuyButtonLabel(buyButton);
            }
        }

        static void ApplyBuyButtonLabel(Button buyButton)
        {
            foreach (var tmp in buyButton.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                tmp.text = Loc.Choose(BuyLabelZh, BuyLabelEn);
                var font = Resources.Load<TMP_FontAsset>("Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF");
                if (font != null)
                {
                    tmp.font = font;
                    tmp.fontSharedMaterial = font.material;
                }
                return;
            }

            foreach (var leg in buyButton.GetComponentsInChildren<Text>(true))
            {
                leg.text = Loc.Choose(BuyLabelZh, BuyLabelEn);
                var f = Resources.Load<Font>("Fonts/AlibabaPuHuiTi-3-75-SemiBold");
                if (f != null)
                    leg.font = f;
                return;
            }
        }

        private void OnBuyClicked()
        {
            OnBuyClick?.Invoke(_entry);
        }

        public event Action<ProductEntry> OnBuyClick;

        static string BuildFoodEffectLabel(ProductEntry entry)
        {
            if (entry == null || !entry.isConsumableFood)
                return string.Empty;

            var parts = new System.Collections.Generic.List<string>();
            if (entry.healAmount > 0.01f)
                parts.Add(Loc.F("回血+{0}", "HP +{0}", Mathf.RoundToInt(entry.healAmount)));
            if (entry.staminaRecover > 0.01f)
                parts.Add(Loc.F("体力+{0}", "Stamina +{0}", Mathf.RoundToInt(entry.staminaRecover)));
            if (entry.speedBoostMultiplier > 1.001f && entry.speedBoostDuration > 0.01f)
            {
                int pct = Mathf.RoundToInt((entry.speedBoostMultiplier - 1f) * 100f);
                parts.Add(Loc.F("移速+{0}%/{1}s", "Speed +{0}%/{1}s", pct, Mathf.RoundToInt(entry.speedBoostDuration)));
            }
            return string.Join("  ", parts);
        }
    }
}
