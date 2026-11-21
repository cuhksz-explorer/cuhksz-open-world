using UnityEngine;

[DisallowMultipleComponent]
public class Qcontrol : MonoBehaviour
{
    [Header("Animator 设置")]
    public Animator animator;

    [Tooltip("Idle 状态 Trigger 名（可以留空）")]
    public string idleTrigger = "Idle";

    [Tooltip("高兴 / 打招呼 Trigger 名")]
    public string happyTrigger = "Wave";

    [Tooltip("生气 Trigger 名")]
    public string angryTrigger = "Angry";

    private void Awake()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    /// <summary>
    /// 由 LLMChatClientLocalQwen 调用，根据 emotion 播放不同动画
    /// </summary>
    public void PlayEmotion(string emotion)
    {
        if (animator == null) return;

        string emo = (emotion ?? "").ToLower();

        // 先清掉所有 trigger，避免冲突
        if (!string.IsNullOrEmpty(idleTrigger)) animator.ResetTrigger(idleTrigger);
        if (!string.IsNullOrEmpty(happyTrigger)) animator.ResetTrigger(happyTrigger);
        if (!string.IsNullOrEmpty(angryTrigger)) animator.ResetTrigger(angryTrigger);

        switch (emo)
        {
            case "happy":
            case "wave":
            case "greet":
            case "greeting":
                if (!string.IsNullOrEmpty(happyTrigger))
                    animator.SetTrigger(happyTrigger);
                break;

            case "angry":
            case "mad":
            case "annoyed":
                if (!string.IsNullOrEmpty(angryTrigger))
                    animator.SetTrigger(angryTrigger);
                break;

            default:
                if (!string.IsNullOrEmpty(idleTrigger))
                    animator.SetTrigger(idleTrigger);
                break;
        }
    }
}

