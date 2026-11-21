using TMPro;
using UnityEngine;

/// <summary>
/// Attach to scene TMP objects with bilingual serialized labels.
/// Refreshes on language change.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [TextArea(1, 4)] [SerializeField] string textZh;
    [TextArea(1, 4)] [SerializeField] string textEn;

    TMP_Text _tmp;

    void Awake() => _tmp = GetComponent<TMP_Text>();

    void OnEnable()
    {
        Loc.OnLanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable() => Loc.OnLanguageChanged -= Refresh;

    public void SetTexts(string zh, string en)
    {
        textZh = zh;
        textEn = en;
        Refresh();
    }

    void Refresh()
    {
        if (_tmp == null)
            _tmp = GetComponent<TMP_Text>();
        if (_tmp != null)
            _tmp.text = Loc.Choose(textZh, textEn);
    }
}
