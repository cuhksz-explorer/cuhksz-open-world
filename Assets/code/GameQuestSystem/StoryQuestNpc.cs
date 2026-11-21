using UnityEngine;
using TMPro;

namespace GameQuestSystem
{
    public class StoryQuestNpc : MonoBehaviour
    {
        [Header("剧情")]
        public string documentId;
        [Tooltip("支线接取触发用 NPC ID（与 QuestAcceptTrigger NpcDialogue 的 triggerId 对应）；为空则用 documentId")]
        public string questAcceptNpcId;
        public string displayName = "NPC";
        public Transform triggerPoint;
        public float interactDistance = 6f;
        public KeyCode interactKey = KeyCode.F;
        public bool hideAfterStoryCompleted = true;

        [Header("外观")]
        [Tooltip("运行时隐藏预制体模型，仅保留交互位置与任务引导（走近仍显示 F 提示）。剧情触发点默认隐藏，角色 NPC 默认显示。")]
        public bool hidePrefabVisualInPlayMode;

        [Header("提示")]
        public bool drawGizmos = true;
        public bool useSharedInteractPrompt = true;
        public bool autoFindSharedPromptIfUnassigned = true;
        public RectTransform sharedPromptPanel;
        public TextMeshProUGUI sharedPromptNameText;
        public TextMeshProUGUI sharedPromptTypeText;
        public int promptPriority = 150;

        Transform _player;
        int _sharedPromptGroupId;

        public Vector3 TriggerPosition => triggerPoint != null ? triggerPoint.position : transform.position;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? "NPC" : displayName;
        public string NpcIdForQuestTriggers =>
            string.IsNullOrWhiteSpace(questAcceptNpcId) ? documentId : questAcceptNpcId.Trim();

        void Awake()
        {
            ResolveSharedPromptRefs();
            ApplyTriggerOnlyAppearanceIfNeeded();
        }

        void OnEnable()
        {
            ApplyTriggerOnlyAppearanceIfNeeded();
            RefreshSharedPromptBindings();
        }

        public void RebindSharedPrompt(RectTransform panel, TextMeshProUGUI nameTmp, TextMeshProUGUI typeTmp)
        {
            sharedPromptPanel = panel;
            sharedPromptNameText = nameTmp;
            sharedPromptTypeText = typeTmp;
            useSharedInteractPrompt = true;
            RefreshSharedPromptBindings();
        }

        public void RefreshSharedPromptBindings()
        {
            _sharedPromptGroupId = 0;
            if (!useSharedInteractPrompt)
                return;

            if (autoFindSharedPromptIfUnassigned
                || sharedPromptPanel == null
                || sharedPromptNameText == null
                || sharedPromptTypeText == null)
            {
                InteractPromptSharedUiLocator.TryAutoBind(
                    ref sharedPromptPanel,
                    ref sharedPromptNameText,
                    ref sharedPromptTypeText);
            }

            _sharedPromptGroupId = InteractPromptSharedLabelFrame.ResolveOfferGroupId(
                sharedPromptNameText,
                sharedPromptTypeText);
        }

        public void ApplyTriggerOnlyAppearanceIfNeeded()
        {
            if (!StoryQuestNpcVisualUtility.IsStoryTriggerDisplayName(DisplayName))
                hidePrefabVisualInPlayMode = false;

            StoryQuestNpcVisualUtility.ApplyTriggerOnlyAppearance(gameObject, hidePrefabVisualInPlayMode);
        }

        void Update()
        {
            var runtime = StoryQuestRuntime.Instance;
            if (runtime == null || string.IsNullOrWhiteSpace(documentId))
                return;

            if (!runtime.CanStartDocument(documentId))
                return;

            if (_player == null)
            {
                var playerTransform = runtime.GetPlayerTransform();
                if (playerTransform != null)
                    _player = playerTransform;
            }
            if (_player == null)
                return;

            float d = Vector3.Distance(_player.position, TriggerPosition);
            if (d > interactDistance)
                return;

            QueueSharedPromptOffer(d);
            if (Input.GetKeyDown(interactKey))
            {
                InteractionNearestTriggerGate.Submit(interactKey, d, GetInstanceID(), StartStoryFromPrompt, promptPriority);
            }
        }

        public void StartStoryFromPrompt()
        {
            StoryQuestRuntime.Instance?.TryStartDocument(documentId, this);
        }

        void QueueSharedPromptOffer(float distance)
        {
            if (!useSharedInteractPrompt)
                return;

            if (_sharedPromptGroupId == 0
                || sharedPromptPanel == null
                || sharedPromptNameText == null
                || sharedPromptTypeText == null)
                RefreshSharedPromptBindings();
            if (_sharedPromptGroupId == 0)
                return;

            Vector3? anchor = InteractPromptSharedUiLocator.ResolveProximityWorldAnchor(triggerPoint, transform);
            InteractPromptSharedLabelFrame.Offer(
                _sharedPromptGroupId,
                distance,
                Loc.StoryTerm(DisplayName),
                Loc.F("对话 ({0})", "Talk ({0})", interactKey),
                anchor);
        }

        void ResolveSharedPromptRefs() => RefreshSharedPromptBindings();

        public void NotifyStoryCompleted()
        {
            if (hideAfterStoryCompleted)
                gameObject.SetActive(false);
        }

        void OnDrawGizmosSelected()
        {
            if (!drawGizmos)
                return;
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(TriggerPosition, interactDistance);
        }
    }
}
