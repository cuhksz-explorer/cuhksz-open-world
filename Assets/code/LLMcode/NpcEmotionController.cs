using UnityEngine;

/// <summary>
/// 控制 NPC Animator 的 emotion(int) 参数，
/// 并提供字符串到动作的映射：
/// idle / waving / thinking / hands_on_hip / clapping / thankful
/// </summary>
public class NPCEmotionController : MonoBehaviour
{
    [Header("Animator 设置")]
    public Animator animator;
    public string emotionParamName = "emotion";

    [Header("各情绪对应的数值（需要和 Animator 条件一致）")]
    public int idleValue = 0;
    public int wavingValue = 1;
    public int thinkingValue = 2;
    public int handsOnHipValue = 3;
    public int clappingValue = 4;
    public int thankfulValue = 5;

    int EmotionHash => Animator.StringToHash(emotionParamName);

    void SetEmotion(int value)
    {
        if (animator == null) return;
        animator.SetInteger(EmotionHash, value);
    }

    // ---- 提供给对话脚本调用的接口 ----

    /// <summary>对话刚被触发：挥手</summary>
    public void TriggerStartConversation()
    {
        SetEmotion(wavingValue);
    }

    /// <summary>流式输出中：思考</summary>
    public void TriggerThinking()
    {
        SetEmotion(thinkingValue);
    }

    /// <summary>出错 / 生气 / 不耐烦</summary>
    public void TriggerAngryOrConfused()
    {
        SetEmotion(handsOnHipValue);
    }

    /// <summary>从大模型返回的字符串映射到具体动作</summary>
    public void SetEmotionFromString(string emotion)
    {
        if (string.IsNullOrEmpty(emotion))
        {
            ResetToIdle();
            return;
        }

        // 统一成小写 + 去掉空格/连字符/下划线
        string e = emotion.ToLowerInvariant()
                          .Replace(" ", "")
                          .Replace("-", "")
                          .Replace("_", "");

        switch (e)
        {
            case "idle":
                SetEmotion(idleValue);
                break;

            case "waving":
                SetEmotion(wavingValue);
                break;

            case "thinking":
                SetEmotion(thinkingValue);
                break;

            case "handsonhip":
            case "hips":
            case "angry":
            case "impatient":
            case "stop":
            case "end":
                SetEmotion(handsOnHipValue);
                break;

            case "clapping":
            case "applause":
            case "agree":
                SetEmotion(clappingValue);
                break;

            case "thankful":
            case "thanks":
            case "grateful":
                SetEmotion(thankfulValue);
                break;

            default:
                // 未知情绪 → 回到 idle
                ResetToIdle();
                break;
        }
    }

    /// <summary>回到 Idle</summary>
    public void ResetToIdle()
    {
        SetEmotion(idleValue);
    }
}


