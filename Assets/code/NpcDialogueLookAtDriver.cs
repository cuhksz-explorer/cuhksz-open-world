using GameQuestSystem;
using UnityEngine;

/// <summary>
/// 对话/剧情交互期间：暂停 NPC 游走，并让所有 NPC 水平转向主角。
/// </summary>
[DefaultExecutionOrder(900)]
public class NpcDialogueLookAtDriver : MonoBehaviour
{
    public static NpcDialogueLookAtDriver Instance { get; private set; }

    [SerializeField] float rotateSpeed = 540f;
    [SerializeField] bool snapRotationOnOpen = true;

    Transform _player;
    PlayerMovement _playerMovement;
    StoryQuestNpc[] _cachedStoryNpcs;
    NpcDialogueInteractor[] _cachedDialogueNpcs;
    int _dialogueDepth;
    bool _snapPending;

    public static bool IsDialogueActive => Instance != null && Instance._dialogueDepth > 0;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public static void EnsureExists()
    {
        if (Instance != null)
            return;

        var runtime = StoryQuestRuntime.Instance;
        if (runtime != null)
        {
            if (runtime.GetComponent<NpcDialogueLookAtDriver>() == null)
                runtime.gameObject.AddComponent<NpcDialogueLookAtDriver>();
            return;
        }

        var manager = FindObjectOfType<NpcDialogueUIManager>(true);
        if (manager != null)
        {
            if (manager.GetComponent<NpcDialogueLookAtDriver>() == null)
                manager.gameObject.AddComponent<NpcDialogueLookAtDriver>();
            return;
        }

        if (FindObjectOfType<NpcDialogueLookAtDriver>(true) == null)
        {
            var go = new GameObject(nameof(NpcDialogueLookAtDriver));
            go.AddComponent<NpcDialogueLookAtDriver>();
        }
    }

    public static void NotifyDialogueOpened()
    {
        EnsureExists();
        if (Instance == null)
            return;

        Instance._dialogueDepth++;
        if (Instance._dialogueDepth == 1)
        {
            Instance._snapPending = Instance.snapRotationOnOpen;
            PauseStoryNpcMovement(true);
            Instance.EnsurePlayer();
            Instance.RefreshNpcCache();
            if (Instance._player != null)
                RotateAllNpcsToward(Instance.ResolveDialogueLookTarget(), true);
        }
    }

    public static void NotifyDialogueClosed()
    {
        if (Instance == null)
            return;

        Instance._dialogueDepth = Mathf.Max(0, Instance._dialogueDepth - 1);
        if (Instance._dialogueDepth == 0)
        {
            Instance._cachedStoryNpcs = null;
            Instance._cachedDialogueNpcs = null;
            PauseStoryNpcMovement(false);
        }
    }

    static void PauseStoryNpcMovement(bool pause)
    {
        if (pause)
        {
            foreach (var movement in FindObjectsOfType<StoryQuestNpcMovement>(true))
            {
                if (movement != null)
                    movement.ApplyMovementPolicy(false);
            }
            return;
        }

        StoryQuestNpcMovementRegistry.RefreshScenePolicy();
    }

    void LateUpdate()
    {
        if (_dialogueDepth <= 0)
            return;

        EnsurePlayer();
        if (_player == null)
            return;

        bool instant = _snapPending;
        _snapPending = false;
        RotateAllNpcsToward(ResolveDialogueLookTarget(), instant);
    }

    Vector3 ResolveDialogueLookTarget()
    {
        if (_player == null)
            return Vector3.zero;

        Vector3 target = _player.position;
        if (_playerMovement != null && _playerMovement.firstPersonCamera != null)
        {
            Vector3 camPos = _playerMovement.firstPersonCamera.position;
            target.x = camPos.x;
            target.z = camPos.z;
        }

        return target;
    }

    void EnsurePlayer()
    {
        if (_player != null)
            return;

        var pm = FindObjectOfType<PlayerMovement>(true);
        if (pm != null)
        {
            _player = pm.transform;
            _playerMovement = pm;
        }
    }

    static void RotateAllNpcsToward(Vector3 targetPosition, bool instant)
    {
        if (Instance != null && (Instance._cachedStoryNpcs == null || Instance._cachedDialogueNpcs == null))
            Instance.RefreshNpcCache();

        var storyNpcs = Instance != null && Instance._cachedStoryNpcs != null
            ? Instance._cachedStoryNpcs
            : FindObjectsOfType<StoryQuestNpc>(true);
        for (int i = 0; i < storyNpcs.Length; i++)
        {
            if (storyNpcs[i] != null && storyNpcs[i].gameObject.activeInHierarchy)
                FaceYaw(storyNpcs[i].transform, targetPosition, instant);
        }

        var dialogueNpcs = Instance != null && Instance._cachedDialogueNpcs != null
            ? Instance._cachedDialogueNpcs
            : FindObjectsOfType<NpcDialogueInteractor>(true);
        for (int i = 0; i < dialogueNpcs.Length; i++)
        {
            if (dialogueNpcs[i] != null && dialogueNpcs[i].isActiveAndEnabled)
                FaceYaw(dialogueNpcs[i].transform, targetPosition, instant);
        }
    }

    void RefreshNpcCache()
    {
        _cachedStoryNpcs = FindObjectsOfType<StoryQuestNpc>(true);
        _cachedDialogueNpcs = FindObjectsOfType<NpcDialogueInteractor>(true);
    }

    static void FaceYaw(Transform npc, Vector3 targetPosition, bool instant)
    {
        if (npc == null)
            return;

        Vector3 dir = targetPosition - npc.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f)
            return;

        float yawDelta = ComputeYawDeltaToFace(npc, dir.normalized);
        if (Mathf.Abs(yawDelta) < 0.01f)
            return;

        if (instant || Instance == null)
        {
            npc.rotation = Quaternion.AngleAxis(yawDelta, Vector3.up) * npc.rotation;
            return;
        }

        float maxDegrees = Instance.rotateSpeed * Time.deltaTime;
        float step = Mathf.Clamp(yawDelta, -maxDegrees, maxDegrees);
        npc.rotation = Quaternion.AngleAxis(step, Vector3.up) * npc.rotation;
    }

    static float ComputeYawDeltaToFace(Transform npc, Vector3 lookDir)
    {
        Transform visual = FindNpcVisualTransform(npc);
        if (visual == null)
        {
            Vector3 forward = npc.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector3.SignedAngle(forward.normalized, lookDir, Vector3.up);
        }

        Vector3 localFace = visual.localRotation * Vector3.forward;
        if (UsesInvertedStoryVisual(npc, visual))
            localFace = -localFace;

        Vector3 worldFace = npc.rotation * localFace;
        worldFace.y = 0f;
        if (worldFace.sqrMagnitude < 0.0001f)
        {
            Vector3 forward = npc.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return 0f;
            if (UsesInvertedStoryVisual(npc, visual))
                forward = -forward;
            return Vector3.SignedAngle(forward.normalized, lookDir, Vector3.up);
        }

        worldFace.Normalize();
        return Vector3.SignedAngle(worldFace, lookDir, Vector3.up);
    }

    static bool UsesInvertedStoryVisual(Transform npc, Transform visual)
    {
        if (npc != null && npc.GetComponent<StoryNpcVisualProfile>() != null)
            return true;
        if (visual == null)
            return false;

        Vector3 localForward = visual.localRotation * Vector3.forward;
        localForward.y = 0f;
        return localForward.sqrMagnitude > 0.0001f && localForward.z < -0.25f;
    }

    static Transform FindNpcVisualTransform(Transform npc)
    {
        if (npc == null)
            return null;

        var profile = npc.GetComponent<StoryNpcVisualProfile>();
        if (profile != null)
        {
            var visual = npc.Find(StoryNpcVisualProfile.VisualChildName);
            if (visual != null)
                return visual;
        }

        return npc.Find(StoryNpcVisualProfile.VisualChildName);
    }
}
