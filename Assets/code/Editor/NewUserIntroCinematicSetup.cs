#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// 一键生成「新账号注册后首次进入关卡前」的全屏开场视频 UI（跳过 + 二次确认）。
/// </summary>
public static class NewUserIntroCinematicSetup
{
    const string MenuPath = "Tools/新用户/一键生成开场动画系统（注册后仅播一次）";
    const string AlibabaTmp = "Fonts & Materials/AlibabaPuHuiTi-3-75-SemiBold SDF";

    [MenuItem(MenuPath, false, 200)]
    static void CreateIntroSystem()
    {
        var existing = Object.FindObjectOfType<NewUserIntroCinematic>(true);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("开场动画", "场景中已存在 NewUserIntroCinematic，是否再创建一个？（通常只需一个）", "仍要创建", "取消"))
                return;
        }

        var root = new GameObject("NewUserIntroCinematic");
        Undo.RegisterCreatedObjectUndo(root, "Create NewUser Intro");

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32750;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();
        var canvasGroup = root.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        var vp = root.AddComponent<VideoPlayer>();
        vp.playOnAwake = false;
        vp.isLooping = false;

        var audio = root.AddComponent<AudioSource>();
        audio.playOnAwake = false;

        var full = CreateUiObject("VideoFullScreen", root.transform);
        var fullRt = full.AddComponent<RectTransform>();
        StretchFull(fullRt);
        var raw = full.AddComponent<RawImage>();
        raw.color = Color.white;
        raw.raycastTarget = false;

        var skipGo = CreateUiObject("SkipButton", root.transform);
        var skipRt = skipGo.AddComponent<RectTransform>();
        skipRt.anchorMin = new Vector2(1, 1);
        skipRt.anchorMax = new Vector2(1, 1);
        skipRt.pivot = new Vector2(1, 1);
        skipRt.anchoredPosition = new Vector2(-32, -32);
        skipRt.sizeDelta = new Vector2(200, 56);
        var skipImg = skipGo.AddComponent<Image>();
        skipImg.color = new Color(0.1f, 0.1f, 0.1f, 0.65f);
        var skipBtn = skipGo.AddComponent<Button>();
        var skipTxtGo = CreateUiObject("Text", skipGo.transform);
        var skipTxtRt = skipTxtGo.AddComponent<RectTransform>();
        skipTxtRt.anchorMin = Vector2.zero;
        skipTxtRt.anchorMax = Vector2.one;
        skipTxtRt.offsetMin = skipTxtRt.offsetMax = Vector2.zero;
        var skipTmp = skipTxtGo.AddComponent<TextMeshProUGUI>();
        skipTmp.text = "跳过";
        skipTmp.fontSize = 26;
        skipTmp.alignment = TextAlignmentOptions.Center;
        skipTmp.color = Color.white;
        skipTmp.raycastTarget = false;
        TryFont(skipTmp);

        var confirmRoot = CreateUiObject("SkipConfirmOverlay", root.transform);
        var confirmRootRt = confirmRoot.AddComponent<RectTransform>();
        StretchFull(confirmRootRt);
        var dim = confirmRoot.AddComponent<Image>();
        dim.color = new Color(0, 0, 0, 0.72f);
        dim.raycastTarget = true;
        confirmRoot.SetActive(false);

        var dialog = CreateUiObject("Dialog", confirmRoot.transform);
        var dialogRt = dialog.AddComponent<RectTransform>();
        dialogRt.anchorMin = dialogRt.anchorMax = new Vector2(0.5f, 0.5f);
        dialogRt.sizeDelta = new Vector2(560, 320);
        dialogRt.anchoredPosition = Vector2.zero;
        var dialogBg = dialog.AddComponent<Image>();
        dialogBg.color = new Color(0.12f, 0.12f, 0.14f, 0.98f);

        var titleGo = CreateUiObject("Title", dialog.transform);
        var titleRt = titleGo.AddComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0, 0.62f);
        titleRt.anchorMax = new Vector2(1, 1);
        titleRt.offsetMin = new Vector2(28, 0);
        titleRt.offsetMax = new Vector2(-28, -20);
        var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "跳过动画？";
        titleTmp.fontSize = 32;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = Color.white;
        titleTmp.raycastTarget = false;
        TryFont(titleTmp);

        var bodyGo = CreateUiObject("Body", dialog.transform);
        var bodyRt = bodyGo.AddComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0, 0.28f);
        bodyRt.anchorMax = new Vector2(1, 0.62f);
        bodyRt.offsetMin = new Vector2(32, 0);
        bodyRt.offsetMax = new Vector2(-32, 0);
        var bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
        bodyTmp.text = "当前为重要剧情演出，确认要跳过吗？";
        bodyTmp.fontSize = 24;
        bodyTmp.alignment = TextAlignmentOptions.Center;
        bodyTmp.color = new Color(0.85f, 0.85f, 0.88f);
        bodyTmp.enableWordWrapping = true;
        bodyTmp.raycastTarget = false;
        TryFont(bodyTmp);

        var btnRow = CreateUiObject("Buttons", dialog.transform);
        var rowRt = btnRow.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0, 0);
        rowRt.anchorMax = new Vector2(1, 0.28f);
        rowRt.offsetMin = new Vector2(40, 24);
        rowRt.offsetMax = new Vector2(-40, 16);
        var hlg = btnRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 24;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false;

        var noBtnGo = CreateUiObject("CancelSkip", btnRow.transform);
        var noRt = noBtnGo.AddComponent<RectTransform>();
        noRt.sizeDelta = new Vector2(200, 52);
        noBtnGo.AddComponent<Image>().color = new Color(0.35f, 0.35f, 0.38f);
        var noBtn = noBtnGo.AddComponent<Button>();
        var noTxt = CreateTmpChild(noBtnGo.transform, "Text", "返回", 24, Color.white);

        var yesBtnGo = CreateUiObject("ConfirmSkip", btnRow.transform);
        var yesRt = yesBtnGo.AddComponent<RectTransform>();
        yesRt.sizeDelta = new Vector2(200, 52);
        yesBtnGo.AddComponent<Image>().color = new Color(0.55f, 0.2f, 0.25f);
        var yesBtn = yesBtnGo.AddComponent<Button>();
        var yesTxt = CreateTmpChild(yesBtnGo.transform, "Text", "确认跳过", 24, Color.white);

        var ctrl = root.AddComponent<NewUserIntroCinematic>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("rootCanvas").objectReferenceValue = canvas;
        so.FindProperty("rootCanvasGroup").objectReferenceValue = canvasGroup;
        so.FindProperty("videoRawImage").objectReferenceValue = raw;
        so.FindProperty("videoPlayer").objectReferenceValue = vp;
        so.FindProperty("videoAudioSource").objectReferenceValue = audio;
        so.FindProperty("skipButton").objectReferenceValue = skipBtn;
        so.FindProperty("confirmPanel").objectReferenceValue = confirmRoot;
        so.FindProperty("confirmSkipButton").objectReferenceValue = yesBtn;
        so.FindProperty("cancelSkipButton").objectReferenceValue = noBtn;
        so.FindProperty("confirmTitleText").objectReferenceValue = titleTmp;
        so.FindProperty("confirmBodyText").objectReferenceValue = bodyTmp;
        so.FindProperty("confirmYesText").objectReferenceValue = yesTxt;
        so.FindProperty("confirmNoText").objectReferenceValue = noTxt;
        so.ApplyModifiedPropertiesWithoutUndo();

        Selection.activeGameObject = root;
        EditorUtility.DisplayDialog(
            "开场动画",
            "已创建 NewUserIntroCinematic。\n\n" +
            "1. 在 Inspector 里将「开场视频」拖到 Intro Video Clip，或填写 StreamingAssets 下的文件名。\n" +
            "2. 确保场景中有 GameServerSync；仅「注册成功」会触发播放，「登录」不会。\n" +
            "3. 排除场景列表默认含 Loadingscene / Menu，进入关卡场景后自动播放一次。\n" +
            "4. 勾选 Editor Simulate Pending Intro 可在编辑器内模拟（无需注册）。",
            "确定");
    }

    static GameObject CreateUiObject(string name, Transform parent)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, name);
        go.transform.SetParent(parent, false);
        return go;
    }

    static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI CreateTmpChild(Transform parent, string name, string text, float size, Color c)
    {
        var go = CreateUiObject(name, parent);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = c;
        tmp.raycastTarget = false;
        TryFont(tmp);
        return tmp;
    }

    static void TryFont(TextMeshProUGUI tmp)
    {
        var f = Resources.Load<TMP_FontAsset>(AlibabaTmp);
        if (f != null)
        {
            tmp.font = f;
            tmp.fontSharedMaterial = f.material;
        }
    }
}
#endif
