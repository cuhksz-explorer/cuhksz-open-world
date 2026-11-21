using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("Optional root for SetBarVisible; defaults to this GameObject.")]
    [SerializeField] private GameObject visibilityRoot;

    void Start()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>();

        if (healthSlider == null)
            healthSlider = GetComponent<Slider>();

        if (healthText == null)
            healthText = BarAmountLabelUtil.FindLabel(transform, "HPamount", "HPAmount");

        ForceBarNonDraggable();

        if (healthSlider != null && healthText != null)
            BarAmountLabelUtil.AttachLabelToHandle(healthText, healthSlider, new Vector2(-4f, 0f));

        if (playerMovement != null)
        {
            playerMovement.OnHealthChanged.AddListener(UpdateHealthBar);
            UpdateHealthBar(playerMovement.GetHealthRatio());
        }
        else
        {
            Debug.LogError("HealthBarUI: PlayerMovement is not assigned.");
        }
    }

    void ForceBarNonDraggable()
    {
        if (healthSlider == null) return;
        healthSlider.interactable = false;
        var handle = healthSlider.handleRect;
        if (handle == null) return;
        var g = handle.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;
    }

    void UpdateHealthBar(float healthRatio)
    {
        if (healthSlider != null)
            healthSlider.value = healthRatio;

        UpdateHealthText();
    }

    void UpdateHealthText()
    {
        if (healthText == null || playerMovement == null) return;

        float currentHealth = playerMovement.currentHealth;
        float maxHealth = playerMovement.maxHealth;
        healthText.text = $"{Mathf.RoundToInt(currentHealth)}/{Mathf.RoundToInt(maxHealth)}";
        healthText.color = currentHealth / maxHealth <= 0.3f ? Color.red : Color.white;
    }

    static bool IsUnderTransform(Transform t, Transform ancestor)
    {
        if (t == null || ancestor == null) return false;
        for (Transform p = t; p != null; p = p.parent)
        {
            if (p == ancestor) return true;
        }
        return false;
    }

    public void SetBarVisible(bool visible)
    {
        var root = visibilityRoot != null ? visibilityRoot : gameObject;
        if (root == null) return;

        if (healthText != null)
        {
            bool textInsideRoot = healthText.transform == root.transform
                || IsUnderTransform(healthText.transform, root.transform);
            if (!textInsideRoot)
                healthText.gameObject.SetActive(visible);
        }

        if (healthSlider != null)
        {
            bool sliderInsideRoot = healthSlider.transform == root.transform
                || IsUnderTransform(healthSlider.transform, root.transform);
            if (!sliderInsideRoot)
                healthSlider.gameObject.SetActive(visible);
        }

        root.SetActive(visible);
    }

    void OnDestroy()
    {
        if (playerMovement != null)
            playerMovement.OnHealthChanged.RemoveListener(UpdateHealthBar);
    }
}
