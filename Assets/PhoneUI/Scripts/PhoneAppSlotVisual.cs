using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PhoneUI
{
    /// <summary>
    /// 九宫格单槽 2.5D 柱体：悬停浅色高亮 + 向上突出。视觉不由 Button ColorBlock 控制。
    /// </summary>
    public class PhoneAppSlotVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public enum SlotVisualState { Idle, Hover, Pressed }

        [Header("References")]
        public RectTransform slotBody;
        public Image slotFace;
        public Image slotSide;
        public Image slotShadow;
        public Shadow faceShadow;

        [Header("Colors")]
        public Color faceColorNormal = new Color(0.88f, 0.89f, 0.93f, 1f);
        public Color faceColorHighlight = new Color(0.97f, 0.98f, 1f, 1f);
        public Color faceColorPressed = new Color(0.92f, 0.94f, 0.98f, 1f);
        public Color sideColorNormal = new Color(0.66f, 0.69f, 0.75f, 1f);
        public Color sideColorHighlight = new Color(0.72f, 0.75f, 0.82f, 1f);

        [Header("Motion")]
        public float liftPixels = 10f;
        public float hoverScale = 1.04f;
        public float pressedLiftFactor = 0.45f;
        public float animDuration = 0.15f;
        public Vector2 shadowDistanceIdle = new Vector2(1f, -2f);
        public Vector2 shadowDistanceHover = new Vector2(2f, -4f);

        SlotVisualState _state = SlotVisualState.Idle;
        Vector2 _bodyRestPosition;
        Vector3 _bodyRestScale = Vector3.one;
        Coroutine _animRoutine;
        bool _pointerInside;

        void Awake()
        {
            if (slotBody != null)
            {
                _bodyRestPosition = slotBody.anchoredPosition;
                _bodyRestScale = slotBody.localScale;
            }
            ApplyInstant(SlotVisualState.Idle);
        }

        void OnDisable()
        {
            _pointerInside = false;
            if (_animRoutine != null)
            {
                StopCoroutine(_animRoutine);
                _animRoutine = null;
            }
            ApplyInstant(SlotVisualState.Idle);
        }

        public void ResetToIdle() => TransitionTo(SlotVisualState.Idle, true);

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (_state != SlotVisualState.Pressed)
                TransitionTo(SlotVisualState.Hover, false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            if (_state != SlotVisualState.Pressed)
                TransitionTo(SlotVisualState.Idle, false);
        }

        public void OnPointerDown(PointerEventData eventData) => TransitionTo(SlotVisualState.Pressed, false);

        public void OnPointerUp(PointerEventData eventData)
        {
            TransitionTo(_pointerInside ? SlotVisualState.Hover : SlotVisualState.Idle, false);
        }

        void TransitionTo(SlotVisualState target, bool instant)
        {
            _state = target;
            if (_animRoutine != null)
                StopCoroutine(_animRoutine);
            if (instant || animDuration <= 0f)
                ApplyInstant(target);
            else
                _animRoutine = StartCoroutine(AnimateTo(target));
        }

        void ApplyInstant(SlotVisualState target)
        {
            float lift = target == SlotVisualState.Idle ? 0f : liftPixels * (target == SlotVisualState.Pressed ? pressedLiftFactor : 1f);
            float scale = target == SlotVisualState.Idle ? 1f : (target == SlotVisualState.Hover ? hoverScale : hoverScale * 0.98f);

            if (slotBody != null)
            {
                slotBody.anchoredPosition = _bodyRestPosition + Vector2.up * lift;
                slotBody.localScale = _bodyRestScale * scale;
            }

            Color face = faceColorNormal;
            Color side = sideColorNormal;
            switch (target)
            {
                case SlotVisualState.Hover:
                    face = faceColorHighlight;
                    side = sideColorHighlight;
                    break;
                case SlotVisualState.Pressed:
                    face = faceColorPressed;
                    side = sideColorHighlight;
                    break;
            }

            if (slotFace != null) slotFace.color = face;
            if (slotSide != null) slotSide.color = side;

            Vector2 shadowDist = target == SlotVisualState.Idle ? shadowDistanceIdle : shadowDistanceHover;
            if (faceShadow != null) faceShadow.effectDistance = shadowDist;
            if (slotShadow != null)
                slotShadow.color = new Color(0f, 0f, 0f, target == SlotVisualState.Idle ? 0.18f : 0.28f);
        }

        IEnumerator AnimateTo(SlotVisualState target)
        {
            float liftTarget = target == SlotVisualState.Idle ? 0f : liftPixels * (target == SlotVisualState.Pressed ? pressedLiftFactor : 1f);
            float scaleTarget = target == SlotVisualState.Idle ? 1f : (target == SlotVisualState.Hover ? hoverScale : hoverScale * 0.98f);

            Vector2 posStart = slotBody != null ? slotBody.anchoredPosition : Vector2.zero;
            Vector2 posEnd = _bodyRestPosition + Vector2.up * liftTarget;
            Vector3 scaleStart = slotBody != null ? slotBody.localScale : Vector3.one;
            Vector3 scaleEnd = _bodyRestScale * scaleTarget;

            Color faceStart = slotFace != null ? slotFace.color : faceColorNormal;
            Color faceEnd = faceColorNormal;
            Color sideStart = slotSide != null ? slotSide.color : sideColorNormal;
            Color sideEnd = sideColorNormal;
            switch (target)
            {
                case SlotVisualState.Hover:
                    faceEnd = faceColorHighlight;
                    sideEnd = sideColorHighlight;
                    break;
                case SlotVisualState.Pressed:
                    faceEnd = faceColorPressed;
                    sideEnd = sideColorHighlight;
                    break;
            }

            float t = 0f;
            while (t < animDuration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / animDuration);
                k = k * k * (3f - 2f * k);
                if (slotBody != null)
                {
                    slotBody.anchoredPosition = Vector2.Lerp(posStart, posEnd, k);
                    slotBody.localScale = Vector3.Lerp(scaleStart, scaleEnd, k);
                }
                if (slotFace != null) slotFace.color = Color.Lerp(faceStart, faceEnd, k);
                if (slotSide != null) slotSide.color = Color.Lerp(sideStart, sideEnd, k);
                yield return null;
            }

            ApplyInstant(target);
            _animRoutine = null;
        }
    }
}
