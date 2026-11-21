using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GameQuestSystem
{
    public partial class QuestSystemUI
    {
        static readonly Color JournalBlack = new Color(0.015f, 0.014f, 0.012f, 0.9f);
        static readonly Color JournalPanelBlack = new Color(0.045f, 0.042f, 0.036f, 0.96f);
        static readonly Color JournalSurfaceBlack = new Color(0.075f, 0.07f, 0.06f, 0.96f);
        static readonly Color JournalGold = new Color(0.88f, 0.68f, 0.32f, 1f);
        static readonly Color JournalSoftGold = new Color(0.95f, 0.82f, 0.48f, 1f);
        static readonly Color JournalWhite = new Color(0.96f, 0.95f, 0.9f, 1f);
        static readonly Color JournalMutedWhite = new Color(0.72f, 0.70f, 0.64f, 1f);

        const int JournalListTitleFont = 20;
        const int JournalListItemFont = 15;
        const int JournalHintFont = 14;
        const int JournalTrackFont = 17;
        const int JournalDistanceFont = 20;
        const int JournalDetailTitleFont = 32;
        const int JournalProgressFont = 24;
        const int JournalSceneFont = 18;
        const int JournalDetailBodyFont = 20;
        const int JournalRewardsFont = 17;
        const int JournalCloseFont = 26;

        void EnsureJournalUiBuilt()
        {
            if (fullPanel == null || journalListRoot != null)
                return;

            var body = fullPanel.transform.Find("Body");
            if (body == null)
                return;

            var listCol = new GameObject("JournalListColumn", typeof(Image));
            listCol.transform.SetParent(body, false);
            var listRt = listCol.GetComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0f, 0f);
            listRt.anchorMax = new Vector2(0.32f, 1f);
            listRt.offsetMin = new Vector2(16f, 16f);
            listRt.offsetMax = new Vector2(-14f, -66f);
            listCol.GetComponent<Image>().color = JournalSurfaceBlack;

            var titleGo = new GameObject("JournalTitle", typeof(Text));
            titleGo.transform.SetParent(listCol.transform, false);
            var titleText = titleGo.GetComponent<Text>();
            titleText.text = Loc.Choose("已接取任务", "Accepted Quests");
            titleText.font = questUIFont != null ? questUIFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = JournalListTitleFont;
            titleText.color = JournalSoftGold;
            titleText.alignment = TextAnchor.UpperLeft;
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -12f);
            titleRt.sizeDelta = new Vector2(0f, 32f);

            var scrollGo = new GameObject("JournalScroll", typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(listCol.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0f, 0f);
            scrollRt.anchorMax = new Vector2(1f, 1f);
            scrollRt.offsetMin = new Vector2(8f, 8f);
            scrollRt.offsetMax = new Vector2(-8f, -50f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.28f);

            var content = new GameObject("JournalListContent", typeof(RectTransform), typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            content.transform.SetParent(scrollGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 10f;
            vlg.padding = new RectOffset(6, 6, 8, 8);
            vlg.childControlHeight = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childForceExpandWidth = true;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            journalListRoot = contentRt;

            var trackGo = new GameObject("TrackButton", typeof(Image), typeof(Button));
            trackGo.transform.SetParent(body, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.36f, 0f);
            trackRt.anchorMax = new Vector2(0.92f, 0f);
            trackRt.pivot = new Vector2(0.5f, 0f);
            trackRt.anchoredPosition = new Vector2(0f, 20f);
            trackRt.sizeDelta = new Vector2(0f, 44f);
            trackGo.GetComponent<Image>().color = JournalGold;
            journalTrackButton = trackGo.GetComponent<Button>();
            journalTrackButton.onClick.AddListener(OnJournalTrackButtonClicked);

            var trackLabel = new GameObject("Label", typeof(Text));
            trackLabel.transform.SetParent(trackGo.transform, false);
            var trackText = trackLabel.GetComponent<Text>();
            trackText.text = Loc.Choose("设为简易栏追踪", "Track in Brief Panel");
            trackText.font = titleText.font;
            trackText.fontSize = JournalTrackFont;
            trackText.alignment = TextAnchor.MiddleCenter;
            trackText.color = Color.black;
            var tlRt = trackLabel.GetComponent<RectTransform>();
            tlRt.anchorMin = Vector2.zero;
            tlRt.anchorMax = Vector2.one;
            tlRt.offsetMin = Vector2.zero;
            tlRt.offsetMax = Vector2.zero;

            var hintGo = new GameObject("JournalHint", typeof(Text));
            hintGo.transform.SetParent(body, false);
            journalHintText = hintGo.GetComponent<Text>();
            journalHintText.text = Loc.Choose(
                "主线仅显示当前进行中的大章；支线显示全部进行中任务。",
                "Main story shows only the current chapter; side quests show all in-progress tasks.");
            journalHintText.font = titleText.font;
            journalHintText.fontSize = JournalHintFont;
            journalHintText.color = JournalMutedWhite;
            journalHintText.alignment = TextAnchor.UpperLeft;
            var hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.36f, 1f);
            hintRt.anchorMax = new Vector2(0.98f, 1f);
            hintRt.pivot = new Vector2(0.5f, 1f);
            hintRt.anchoredPosition = new Vector2(0f, -14f);
            hintRt.sizeDelta = new Vector2(0f, 44f);

            StyleFullText(journalHintText, JournalHintFont, JournalMutedWhite, TextAnchor.UpperLeft, true);

            ShiftDetailTextsForJournal(body);
            ApplyFullPanelJournalStyle();
        }

        static void ShiftDetailTextsForJournal(Transform body)
        {
            void Shift(string childName, float leftMinX)
            {
                var t = body.Find(childName);
                if (t == null) return;
                var rt = t.GetComponent<RectTransform>();
                if (rt == null) return;
                var min = rt.offsetMin;
                min.x = leftMinX;
                rt.offsetMin = min;
            }

            Shift("DistanceText", 300f);
            Shift("TitleText", 300f);
            Shift("ProgressText", 300f);
            Shift("SceneText", 300f);
            Shift("DetailText", 300f);
            Shift("RewardsText", 300f);
        }

        void ApplyFullPanelJournalStyle()
        {
            if (fullPanel == null)
                return;

            var fullImage = fullPanel.GetComponent<Image>();
            if (fullImage != null)
                fullImage.color = JournalBlack;

            var body = fullPanel.transform.Find("Body");
            if (body == null)
                return;

            var bodyImage = body.GetComponent<Image>();
            if (bodyImage != null)
                bodyImage.color = JournalPanelBlack;

            ResolveFullTextReference(ref fullDistanceText, body, "DistanceText");
            ResolveFullTextReference(ref fullTitleText, body, "TitleText");
            ResolveFullTextReference(ref fullProgressText, body, "ProgressText");
            EnsureFullSceneText(body);
            ResolveFullTextReference(ref fullDetailText, body, "DetailText");
            ResolveFullTextReference(ref fullRewardsText, body, "RewardsText");

            EnsureDecorativePanel(body, "RightDetailCard",
                new Vector2(0.345f, 0f), new Vector2(0.985f, 1f),
                new Vector2(0f, 82f), new Vector2(0f, -48f), JournalSurfaceBlack, 0);
            EnsureDecorativePanel(body, "GoldColumnDivider",
                new Vector2(0.335f, 0f), new Vector2(0.335f, 1f),
                new Vector2(0f, 78f), new Vector2(2f, -48f), JournalGold, 1);
            EnsureDecorativePanel(body, "GoldTopRule",
                new Vector2(0.36f, 1f), new Vector2(0.94f, 1f),
                new Vector2(0f, -86f), new Vector2(0f, -83f), JournalGold, 2);

            StyleFullText(fullDistanceText, JournalDistanceFont, JournalSoftGold, TextAnchor.UpperLeft, false);
            StyleFullText(fullTitleText, JournalDetailTitleFont, JournalWhite, TextAnchor.UpperLeft, true);
            StyleFullText(fullProgressText, JournalProgressFont, JournalWhite, TextAnchor.UpperLeft, false);
            StyleFullText(fullSceneText, JournalSceneFont, JournalMutedWhite, TextAnchor.UpperLeft, false);
            StyleFullText(fullDetailText, JournalDetailBodyFont, JournalWhite, TextAnchor.UpperLeft, true);
            StyleFullText(fullRewardsText, JournalRewardsFont, JournalMutedWhite, TextAnchor.UpperLeft, true);

            SetTopStretchRect(fullDistanceText, 300f, -48f, -90f, -84f);
            SetTopStretchRect(fullTitleText, 300f, -92f, -90f, -188f);
            SetTopStretchRect(fullProgressText, 300f, -200f, -90f, -240f);
            SetTopStretchRect(fullSceneText, 300f, -248f, -90f, -276f);
            SetTopStretchRect(fullDetailText, 300f, -288f, -50f, -576f);
            SetTopStretchRect(fullRewardsText, 300f, -586f, -50f, -676f);

            StyleExistingJournalChrome(body);
            StyleCloseButton(body);
        }

        static void ResolveFullTextReference(ref Text text, Transform body, string childName)
        {
            if (body == null)
                return;

            if (text != null && text.transform.IsChildOf(body))
                return;

            var t = body.Find(childName);
            var resolved = t != null ? t.GetComponent<Text>() : null;
            if (resolved != null)
                text = resolved;
        }

        void EnsureFullSceneText(Transform body)
        {
            if (body == null)
                return;

            ResolveFullTextReference(ref fullSceneText, body, "SceneText");
            if (fullSceneText != null)
                return;

            var go = new GameObject("SceneText", typeof(Text));
            go.transform.SetParent(body, false);
            fullSceneText = go.GetComponent<Text>();
            fullSceneText.font = questUIFont != null
                ? questUIFont
                : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            fullSceneText.supportRichText = true;
        }

        static void EnsureDecorativePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax, Color color, int siblingIndex)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name, typeof(Image));
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = t.gameObject;
            }

            var img = go.GetComponent<Image>();
            if (img != null)
            {
                img.color = color;
                img.raycastTarget = false;
            }

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.offsetMin = offsetMin;
                rt.offsetMax = offsetMax;
            }

            go.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.childCount - 1));
        }

        static void StyleFullText(Text text, int fontSize, Color color, TextAnchor alignment, bool overflow)
        {
            if (text == null)
                return;

            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.resizeTextForBestFit = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = overflow ? VerticalWrapMode.Overflow : VerticalWrapMode.Truncate;
            text.supportRichText = true;
        }

        static void SetTopStretchRect(Text text, float left, float top, float right, float bottom)
        {
            if (text == null)
                return;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(right, top);
        }

        void StyleExistingJournalChrome(Transform body)
        {
            var listCol = body.Find("JournalListColumn");
            if (listCol != null)
            {
                var rt = listCol.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 0f);
                    rt.anchorMax = new Vector2(0.32f, 1f);
                    rt.offsetMin = new Vector2(16f, 16f);
                    rt.offsetMax = new Vector2(-14f, -66f);
                }

                var img = listCol.GetComponent<Image>();
                if (img != null)
                    img.color = JournalSurfaceBlack;

                var title = listCol.Find("JournalTitle");
                var titleText = title != null ? title.GetComponent<Text>() : null;
                StyleFullText(titleText, JournalListTitleFont, JournalSoftGold, TextAnchor.UpperLeft, false);

                var scroll = listCol.Find("JournalScroll");
                if (scroll != null)
                {
                    var scrollRt = scroll.GetComponent<RectTransform>();
                    if (scrollRt != null)
                    {
                        scrollRt.anchorMin = new Vector2(0f, 0f);
                        scrollRt.anchorMax = new Vector2(1f, 1f);
                        scrollRt.offsetMin = new Vector2(8f, 8f);
                        scrollRt.offsetMax = new Vector2(-8f, -50f);
                    }

                    var scrollImage = scroll.GetComponent<Image>();
                    if (scrollImage != null)
                        scrollImage.color = new Color(0f, 0f, 0f, 0.28f);
                }
            }

            if (journalHintText != null)
            {
                StyleFullText(journalHintText, JournalHintFont, JournalMutedWhite, TextAnchor.UpperLeft, true);
                var rt = journalHintText.rectTransform;
                rt.anchorMin = new Vector2(0.36f, 1f);
                rt.anchorMax = new Vector2(0.98f, 1f);
                rt.anchoredPosition = new Vector2(0f, -14f);
                rt.sizeDelta = new Vector2(0f, 44f);
            }

            if (journalTrackButton != null)
            {
                var rt = journalTrackButton.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.36f, 0f);
                    rt.anchorMax = new Vector2(0.92f, 0f);
                    rt.anchoredPosition = new Vector2(0f, 20f);
                    rt.sizeDelta = new Vector2(0f, 44f);
                }

                var img = journalTrackButton.GetComponent<Image>();
                if (img != null)
                    img.color = JournalGold;
                var label = journalTrackButton.GetComponentInChildren<Text>();
                StyleFullText(label, JournalTrackFont, Color.black, TextAnchor.MiddleCenter, false);
            }
        }

        void StyleCloseButton(Transform body)
        {
            var close = body.Find("CloseButton");
            if (close == null)
                return;

            var img = close.GetComponent<Image>();
            if (img != null)
                img.color = JournalGold;

            var rt = close.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(-22f, -22f);
                rt.sizeDelta = new Vector2(54f, 54f);
            }

            var label = close.GetComponentInChildren<Text>();
            StyleFullText(label, JournalCloseFont, Color.black, TextAnchor.MiddleCenter, false);
        }

        void RebuildJournalList()
        {
            if (journalListRoot == null || questManager == null)
                return;

            for (int i = 0; i < _journalListItemInstances.Count; i++)
            {
                if (_journalListItemInstances[i] != null)
                    Destroy(_journalListItemInstances[i]);
            }

            _journalListItemInstances.Clear();

            var accepted = questManager.GetJournalQuests();
            if (accepted.Count == 0)
            {
                _journalSelectedQuestId = "";
                RefreshJournalDetailPanel(null, null);
                return;
            }

            bool selectedStillValid = false;
            for (int i = 0; i < accepted.Count; i++)
            {
                if (accepted[i] != null &&
                    string.Equals(accepted[i].questId, _journalSelectedQuestId, System.StringComparison.Ordinal))
                {
                    selectedStillValid = true;
                    break;
                }
            }

            if (!selectedStillValid)
                _journalSelectedQuestId = accepted[0].questId;

            for (int i = 0; i < accepted.Count; i++)
                CreateJournalListItem(accepted[i]);

            if (string.IsNullOrEmpty(_journalSelectedQuestId))
                RefreshJournalDetailPanel(null, null);
            else
            {
                var def = FindQuestDef(_journalSelectedQuestId);
                var sub = questManager.GetSubTaskForQuest(_journalSelectedQuestId);
                RefreshJournalDetailPanel(def, sub);
            }

            UpdateJournalTrackButtonState();
        }

        QuestChainDefinition FindQuestDef(string questId)
        {
            if (questManager == null || string.IsNullOrEmpty(questId))
                return null;
            var chains = questManager.QuestChains;
            for (int i = 0; i < chains.Count; i++)
            {
                if (chains[i] != null && string.Equals(chains[i].questId, questId, System.StringComparison.Ordinal))
                    return chains[i];
            }

            return null;
        }

        void CreateJournalListItem(QuestChainDefinition quest)
        {
            if (quest == null || journalListRoot == null)
                return;

            bool isTracked = questManager != null &&
                string.Equals(questManager.TrackedQuestId, quest.questId, System.StringComparison.Ordinal);
            bool isSelected = string.Equals(_journalSelectedQuestId, quest.questId, System.StringComparison.Ordinal);

            var go = new GameObject("QuestItem_" + quest.questId, typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(journalListRoot, false);
            _journalListItemInstances.Add(go);

            var img = go.GetComponent<Image>();
            img.color = isSelected
                ? new Color(0.88f, 0.68f, 0.32f, 0.98f)
                : new Color(0.035f, 0.033f, 0.03f, 0.94f);

            var le = go.GetComponent<LayoutElement>();
            le.minHeight = 64f;
            le.preferredHeight = 64f;

            var label = new GameObject("Label", typeof(Text));
            label.transform.SetParent(go.transform, false);
            var text = label.GetComponent<Text>();
            string tag = CategoryPrefix(quest.category);
            string trackMark = isTracked ? " ★" : "";
            text.text = tag + " " + quest.questName + trackMark;
            text.font = questUIFont != null ? questUIFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = JournalListItemFont;
            text.color = isSelected ? Color.black : JournalWhite;
            text.alignment = TextAnchor.MiddleLeft;
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(12f, 6f);
            lrt.offsetMax = new Vector2(-12f, -6f);

            string capturedId = quest.questId;
            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                _journalSelectedQuestId = capturedId;
                RebuildJournalList();
            });
        }

        void RefreshJournalDetailPanel(QuestChainDefinition quest, QuestSubTask sub)
        {
            if (!_isFullOpen)
                return;

            if (quest == null || sub == null)
            {
                if (fullTitleText != null)
                    fullTitleText.text = Loc.Choose("请选择左侧已接取任务", "Select an accepted quest on the left");
                if (fullProgressText != null) fullProgressText.text = "--/--";
                if (fullDetailText != null)
                    fullDetailText.text = Loc.Choose(
                        "仅显示已接取且未完成的任务。\n已完成任务不会出现在左侧列表。\n简易任务栏默认追踪主线；可在此选择支线并点击「设为简易栏追踪」。",
                        "Shows accepted, incomplete quests only.\nCompleted quests are hidden from the list.\nThe brief panel tracks main story by default; select a side quest here and tap Track in Brief Panel.");
                if (fullRewardsText != null) fullRewardsText.text = "";
                if (fullDistanceText != null) fullDistanceText.text = Loc.Choose("距离目标: --", "Distance: --");
                if (fullSceneText != null) fullSceneText.text = QuestSceneDisplayUtil.FormatQuestSceneLine(null, false);
                return;
            }

            string categoryPrefix = CategoryPrefix(quest.category);
            if (fullTitleText != null)
                fullTitleText.text = StoryProtagonistNameUtility.PersonalizeStoryText(
                    categoryPrefix + " " + sub.subTaskName + " - " + sub.finishLocationName);

            int step = questManager.GetDisplaySubTaskIndexForQuest(quest.questId);
            int total = Mathf.Max(1, questManager.GetTotalSubTaskCountForQuest(quest.questId));
            if (fullProgressText != null)
                fullProgressText.text = step + "/" + total;

            if (fullSceneText != null)
                fullSceneText.text = FormatQuestSceneText(sub);

            float dist = questManager.GetDistanceToQuestTarget(quest.questId);
            if (fullDistanceText != null)
                fullDistanceText.text = FormatDistanceText(dist);

            string longDesc = StoryProtagonistNameUtility.PersonalizeStoryText(sub.longDescription);
            if (sub.triggerType == QuestTriggerType.Counter)
            {
                int cur = questManager.GetCounterCurrentForQuest(quest.questId);
                int need = Mathf.Max(1, sub.targetCount);
                longDesc += "\n\n" + Loc.F("当前计数进度: {0}/{1}", "Counter progress: {0}/{1}", cur, need);
            }

            if (fullDetailText != null)
                fullDetailText.text = longDesc;
            if (fullRewardsText != null)
                fullRewardsText.text = FormatRewardsText(sub.rewardGold, quest.finalRewardGold);

            UpdateJournalTrackButtonState();
        }

        void UpdateJournalTrackButtonState()
        {
            if (journalTrackButton == null || questManager == null)
                return;

            bool canTrack = !string.IsNullOrEmpty(_journalSelectedQuestId) &&
                questManager.IsQuestAccepted(_journalSelectedQuestId) &&
                !questManager.IsQuestCompleted(_journalSelectedQuestId);

            var def = FindQuestDef(_journalSelectedQuestId);
            if (def != null && def.category == QuestCategory.Main)
            {
                if (!questManager.IsMainQuestUnlocked(def) || !questManager.IsCurrentMainStoryChapter(def))
                    canTrack = false;
            }

            journalTrackButton.interactable = canTrack;
            bool isCurrent = string.Equals(questManager.TrackedQuestId, _journalSelectedQuestId,
                System.StringComparison.Ordinal);
            var label = journalTrackButton.GetComponentInChildren<Text>();
            if (label != null)
                label.text = isCurrent
                    ? Loc.Choose("当前简易栏追踪中", "Currently tracked in brief panel")
                    : Loc.Choose("设为简易栏追踪", "Track in Brief Panel");
        }

        void RefreshJournalLocalizedText()
        {
            if (fullPanel == null)
                return;

            var body = fullPanel.transform.Find("Body");
            if (body == null)
                return;

            var listCol = body.Find("JournalListColumn");
            if (listCol != null)
            {
                var title = listCol.Find("JournalTitle");
                var titleText = title != null ? title.GetComponent<Text>() : null;
                if (titleText != null)
                    titleText.text = Loc.Choose("已接取任务", "Accepted Quests");
            }

            if (journalHintText != null)
            {
                journalHintText.text = Loc.Choose(
                    "主线仅显示当前进行中的大章；支线显示全部进行中任务。",
                    "Main story shows only the current chapter; side quests show all in-progress tasks.");
            }

            if (_isFullOpen && !string.IsNullOrEmpty(_journalSelectedQuestId) && questManager != null)
            {
                var quest = FindQuestDef(_journalSelectedQuestId);
                var sub = questManager.GetSubTaskForQuest(_journalSelectedQuestId);
                if (fullSceneText != null)
                    fullSceneText.text = FormatQuestSceneText(sub);
            }

            UpdateJournalTrackButtonState();
        }

        void OnJournalTrackButtonClicked()
        {
            if (questManager == null || string.IsNullOrEmpty(_journalSelectedQuestId))
                return;

            questManager.SetTrackedQuest(_journalSelectedQuestId);
            RebuildJournalList();
            RefreshAll();
        }
    }
}
