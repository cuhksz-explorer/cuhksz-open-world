using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StaminaBarUI : MonoBehaviour
{
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("Optional root for SetBarVisible; defaults to this GameObject.")]
    [SerializeField] private GameObject visibilityRoot;

    void Start()
    {
        if (playerMovement == null)
            playerMovement = FindObjectOfType<PlayerMovement>();

        if (staminaSlider == null)
            staminaSlider = GetComponent<Slider>();

        if (staminaText == null)
            staminaText = BarAmountLabelUtil.FindLabel(transform, "STamount", "Staminaamount", "STAmount");

        ForceBarNonDraggable();

        if (staminaSlider != null && staminaText != null)
            BarAmountLabelUtil.AttachLabelToHandle(staminaText, staminaSlider, new Vector2(-4f, 0f));

        if (playerMovement != null)
        {
            playerMovement.OnStaminaChanged.AddListener(UpdateStaminaBar);
            UpdateStaminaBar(playerMovement.GetStaminaRatio());
        }
        else
        {
            Debug.LogError("StaminaBarUI: PlayerMovement is not assigned.");
        }
    }

    void ForceBarNonDraggable()
    {
        if (staminaSlider == null) return;
        staminaSlider.interactable = false;
        var handle = staminaSlider.handleRect;
        if (handle == null) return;
        var g = handle.GetComponent<Graphic>();
        if (g != null) g.raycastTarget = false;
    }

    void UpdateStaminaBar(float staminaRatio)
    {
        if (staminaSlider != null)
            staminaSlider.value = staminaRatio;

        UpdateStaminaText();
    }

    void UpdateStaminaText()
    {
        if (staminaText == null || playerMovement == null) return;

        float currentStamina = playerMovement.currentStamina;
        float maxStamina = playerMovement.maxStamina;
        staminaText.text = $"{Mathf.RoundToInt(currentStamina)}/{Mathf.RoundToInt(maxStamina)}";
        staminaText.color = currentStamina / maxStamina <= 0.3f
            ? new Color(1f, 0.8f, 0f)
            : Color.white;
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

        if (staminaText != null)
        {
            bool textInsideRoot = staminaText.transform == root.transform
                || IsUnderTransform(staminaText.transform, root.transform);
            if (!textInsideRoot)
                staminaText.gameObject.SetActive(visible);
        }

        if (staminaSlider != null)
        {
            bool sliderInsideRoot = staminaSlider.transform == root.transform
                || IsUnderTransform(staminaSlider.transform, root.transform);
            if (!sliderInsideRoot)
                staminaSlider.gameObject.SetActive(visible);
        }

        root.SetActive(visible);
    }

    void OnDestroy()
    {
        if (playerMovement != null)
            playerMovement.OnStaminaChanged.RemoveListener(UpdateStaminaBar);
    }
}
