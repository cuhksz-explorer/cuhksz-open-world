using GameQuestSystem;
using Mirror;
using UnityEngine;

/// <summary>
/// 挂在 CoopPlayer 上：联机客端通过 Command 在主机服务器上推进 <see cref="QuestSystemManager"/>。
/// </summary>
[DisallowMultipleComponent]
public class CoopQuestRelay : NetworkBehaviour
{
    public static CoopQuestRelay Instance { get; private set; }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        Instance = this;
    }

    public override void OnStopLocalPlayer()
    {
        if (Instance == this)
            Instance = null;
        base.OnStopLocalPlayer();
    }

    public void RequestCompleteActiveSubTask() => CmdRequestCompleteActiveSubTask();

    public void ReportQuestEvent(string eventId) => CmdReportQuestEvent(eventId);

    public void ReportQuestCounter(string counterId, int add) => CmdReportQuestCounter(counterId, add);

    [Command]
    void CmdRequestCompleteActiveSubTask()
    {
        QuestSystemManager.Instance?.ServerApplyRelayCompleteActiveSubTask(connectionToClient);
    }

    [Command]
    void CmdReportQuestEvent(string eventId)
    {
        QuestSystemManager.Instance?.ServerApplyRelayReportEvent(eventId);
    }

    [Command]
    void CmdReportQuestCounter(string counterId, int add)
    {
        QuestSystemManager.Instance?.ServerApplyRelayReportCounter(counterId, add);
    }
}
