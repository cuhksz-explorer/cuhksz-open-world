using System.Threading.Tasks;
using DormMall;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class TeleportButton
{
    [Header("=== 寝室 ===")]
    public DormTeleportRole dormRole = DormTeleportRole.None;
    public DormReturnAnchor dormReturnAnchor;
    public string dormExitFallbackSceneName = DormSceneConfig.DefaultFallbackReturnScene;
    public Vector3 dormExitFallbackPosition = DormSceneConfig.DefaultFallbackReturnPosition;
    public Vector3 dormExitFallbackRotation = DormSceneConfig.DefaultFallbackReturnRotation;

    public static void RecordDormReturnPoint(MonoBehaviour host)
    {
        var teleport = host as TeleportButton ?? host.GetComponent<TeleportButton>();
        if (teleport != null
            && teleport.dormReturnAnchor != null
            && teleport.dormReturnAnchor.TryGetReturnPoint(out Vector3 pos, out Quaternion rot))
        {
            DormReturnState.RecordBeforeEnteringDorm(SceneManager.GetActiveScene().name, pos, rot);
            return;
        }

        DormReturnState.RecordFromCurrentPlayer(
            teleport != null && teleport.dormReturnAnchor != null
                ? teleport.dormReturnAnchor.transform
                : null);
    }

    public async Task RunExitDormAsync()
    {
        GameplayCameraInputRestorer.MarkLeavingDorm();

        string sceneName;
        Vector3 pos;
        Vector3 rotEuler;
        if (!DormReturnState.TryGetReturn(out sceneName, out pos, out rotEuler))
        {
            sceneName = dormExitFallbackSceneName;
            pos = dormExitFallbackPosition;
            rotEuler = dormExitFallbackRotation;
        }

        targetSceneName = sceneName;
        targetPosition = pos;
        targetRotation = rotEuler;

        bool useCrossSceneOverlay = useLoadVideo && loadVideoClip != null;
        try
        {
            ShowLoadingUI(skipInSceneVideo: useCrossSceneOverlay);
            await SceneTeleportHelper.LoadSceneAsync(sceneName, BuildLoadUiOptions(), this);
            SceneTeleportHelper.ApplyPlayerTransform(pos, rotEuler);
            if (SceneTeleportHelper.TryFindLocalPlayer(out var player))
            {
                var movement = player.GetComponent<PlayerMovement>();
                if (movement != null)
                    MapTeleportCameraSnap.AfterPlayerMoved(movement);
            }
        }
        finally
        {
            TeleportSceneLoadOverlay.Hide();
            HideLoadingUI();
            GameplayCameraInputRestorer.ScheduleRestore(6);
        }
    }

    SceneTeleportHelper.LoadUiOptions BuildLoadUiOptions()
    {
        return new SceneTeleportHelper.LoadUiOptions
        {
            useProgressBar = useProgressBar,
            loadProgressSlider = loadProgressSlider,
            progressText = progressText,
            useLoadVideo = useLoadVideo,
            videoPanel = videoPanel,
            videoRawImage = videoRawImage,
            loadVideoClipVariants = null,
            loadVideoClip = loadVideoClip,
            loopVideo = loopVideo,
            videoVolume = videoVolume
        };
    }
}
