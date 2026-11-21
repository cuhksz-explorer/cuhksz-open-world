using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 协议整行 TMP 链接点击（《用户协议》《隐私政策》）。
/// </summary>
public class LguHubAgreementLineClick : MonoBehaviour, IPointerClickHandler
{
    TMP_Text _text;

    public event Action<string> LinkClicked;

    void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_text == null)
            return;

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(_text, eventData.position, eventData.pressEventCamera);
        if (linkIndex < 0)
            return;

        string linkId = _text.textInfo.linkInfo[linkIndex].GetLinkID();
        if (!string.IsNullOrEmpty(linkId))
            LinkClicked?.Invoke(linkId);
    }
}
