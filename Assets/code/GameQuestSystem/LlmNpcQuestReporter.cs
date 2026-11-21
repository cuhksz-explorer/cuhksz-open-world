using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// LLM NPC 完成一轮对话后上报任务事件。
    /// </summary>
    [DisallowMultipleComponent]
    public class LlmNpcQuestReporter : MonoBehaviour
    {
        [Tooltip("与 QuestAcceptTrigger NpcDialogue 的 triggerId 对应")]
        public string npcId = "";

        LLMChatClient_LocalQwen _chat;
        bool _subscribed;

        void Reset()
        {
            if (string.IsNullOrWhiteSpace(npcId))
                npcId = gameObject.name;
        }

        void Awake()
        {
            if (string.IsNullOrWhiteSpace(npcId))
                npcId = gameObject.name;
        }

        void OnEnable()
        {
            BindChat();
        }

        void OnDisable()
        {
            UnbindChat();
        }

        public void BindChatClient(LLMChatClient_LocalQwen chat)
        {
            UnbindChat();
            _chat = chat;
            BindChat();
        }

        void BindChat()
        {
            if (_subscribed || _chat == null)
                return;
            _chat.OnTurnFinished += OnTurnFinished;
            _subscribed = true;
        }

        void UnbindChat()
        {
            if (!_subscribed || _chat == null)
                return;
            _chat.OnTurnFinished -= OnTurnFinished;
            _subscribed = false;
        }

        void OnTurnFinished(string user, string assistant)
        {
            if (string.IsNullOrWhiteSpace(assistant))
                return;

            var mgr = QuestSystemManager.Instance;
            if (mgr != null)
                mgr.ReportEvent(LlmWanderingNpcQuestPreset.TalkCompleteEventId);

            QuestSideQuestAcceptListener.ReportNpcDialogue(npcId);
            Debug.Log("[LlmNpcQuest] 对话完成，上报 " + LlmWanderingNpcQuestPreset.TalkCompleteEventId);
        }
    }
}
