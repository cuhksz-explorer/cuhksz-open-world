using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace GameQuestSystem
{
    public partial class QuestSystemUI
    {
        const string BriefPanelName = "QuestBriefPanel";
        const string FullPanelName = "QuestFullPanel";

        void TryRebindSceneUiReferences()
        {
            if (HasValidBriefPanelBinding())
                return;

            var openPanel = ResolveOpenPanelRoot();
            if (openPanel == null)
                openPanel = FindInLoadedScenesByName(openPanelObjectName);

            if (openPanel != null)
            {
                BindPanelsUnderOpenPanel(openPanel.transform);
                if (HasValidBriefPanelBinding())
                    return;
            }

            BindPanelsFromLoadedScenes();
        }

        bool HasValidBriefPanelBinding()
        {
            return briefPanel != null && briefTitleText != null && briefIntroText != null;
        }

        void BindPanelsUnderOpenPanel(Transform openPanel)
        {
            if (openPanel == null)
                return;

            if (briefPanel == null)
                briefPanel = FindChildGameObjectByName(openPanel, BriefPanelName);
            if (fullPanel == null)
                fullPanel = FindChildGameObjectByName(openPanel, FullPanelName);

            BindBriefTextsFromPanel(briefPanel);
            BindFullTextsFromPanel(fullPanel);

            if (briefPanel != null && briefOpenButton == null)
                briefOpenButton = briefPanel.GetComponent<Button>();
            if (fullPanel != null && fullCloseButton == null)
                fullCloseButton = FindChildComponentByName<Button>(fullPanel.transform, "CloseButton");
        }

        void BindPanelsFromLoadedScenes()
        {
            if (briefPanel == null)
                briefPanel = FindInLoadedScenesByName(BriefPanelName);
            if (fullPanel == null)
                fullPanel = FindInLoadedScenesByName(FullPanelName);

            BindBriefTextsFromPanel(briefPanel);
            BindFullTextsFromPanel(fullPanel);

            if (briefPanel != null && briefOpenButton == null)
                briefOpenButton = briefPanel.GetComponent<Button>();
            if (fullPanel != null && fullCloseButton == null)
                fullCloseButton = FindChildComponentByName<Button>(fullPanel.transform, "CloseButton");
        }

        void BindBriefTextsFromPanel(GameObject panel)
        {
            if (panel == null)
                return;

            var root = panel.transform;
            if (briefDistanceText == null)
                briefDistanceText = FindChildComponentByName<Text>(root, "DistanceText");
            if (briefTitleText == null)
                briefTitleText = FindChildComponentByName<Text>(root, "TitleText");
            if (briefProgressText == null)
                briefProgressText = FindChildComponentByName<Text>(root, "ProgressText");
            if (briefSceneText == null)
                briefSceneText = FindChildComponentByName<Text>(root, "SceneText");
            if (briefIntroText == null)
                briefIntroText = FindChildComponentByName<Text>(root, "IntroText");
        }

        void BindFullTextsFromPanel(GameObject panel)
        {
            if (panel == null)
                return;

            var root = panel.transform;
            if (fullDistanceText == null)
                fullDistanceText = FindChildComponentByName<Text>(root, "DistanceText");
            if (fullTitleText == null)
                fullTitleText = FindChildComponentByName<Text>(root, "TitleText");
            if (fullProgressText == null)
                fullProgressText = FindChildComponentByName<Text>(root, "ProgressText");
            if (fullSceneText == null)
                fullSceneText = FindChildComponentByName<Text>(root, "SceneText");
            if (fullDetailText == null)
                fullDetailText = FindChildComponentByName<Text>(root, "DetailText");
            if (fullRewardsText == null)
                fullRewardsText = FindChildComponentByName<Text>(root, "RewardsText");
        }

        static GameObject FindChildGameObjectByName(Transform root, string objectName)
        {
            var t = FindChildTransformByName(root, objectName);
            return t != null ? t.gameObject : null;
        }

        static T FindChildComponentByName<T>(Transform root, string objectName) where T : Component
        {
            var t = FindChildTransformByName(root, objectName);
            return t != null ? t.GetComponent<T>() : null;
        }
    }
}
