using GameControls;
using UnityEngine;
using UnityEngine.Events;
using DormMall;

// CameraMode 和 MovementState 枚举定义在 PlayerEnums.cs 中

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移动基础设置")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 15f;
    public float runSpeedMultiplier = 1.5f;

    [Header("跳跃设置")]
    public float jumpForce = 7f;
    [Tooltip("相对 Physics.gravity 的下落加速度倍率")]
    public float gravityScale = 2.5f;
    public float jumpStaminaCost = 10f;

    [Header("蹲下设置")]
    public float crouchHeight = 1f;
    public float crouchSpeedMultiplier = 0.7f;
    public float crouchSmoothTime = 0.2f;
    [Tooltip("第一人称视点随蹲下降低的额外比例（1=与胶囊体同比例）")]
    public float crouchCameraHeightRatio = 1f;
    private float originalHeight;
    private Vector3 originalCenter;
    private float currentHeightVelocity;

    [Header("体力系统设置")]
    public float maxStamina = 150f;
    public float staminaConsumeRate = 15f;
    public float staminaRecoverRate = 10f;
    public float staminaRunThreshold = 30f;
    public float currentStamina;
    public bool canRunAgain = true;
    public bool isCurrentlyRunning;
    public float horizontalInput;
    public float verticalInput;
    public UnityEvent OnStaminaEmpty;
    public UnityEvent OnStaminaRecovered;
    public UnityEvent<float> OnStaminaChanged;

    [Header("血量系统设置")]
    public float maxHealth = 150f;
    public float currentHealth;
    public Transform respawnPoint;
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;

    [Header("坠落伤害")]
    [Tooltip("从高处落地时按撞击速度扣血")]
    public bool enableFallDamage = true;
    [Tooltip("落地垂直向下速度低于此值（约 m/s）不扣坠落伤；调高更不敏感")]
    public float safeLandingVerticalSpeed = 16f;
    [Tooltip("超过安全速度部分，每 1 m/s 造成的伤害")]
    public float fallDamagePerExcessSpeed = 3.5f;
    [Tooltip("单次坠落伤害上限")]
    public float maxFallDamage = 75f;
    [Tooltip("计算出的坠落伤害低于此值则完全不扣血（避免小落差蹭伤）")]
    public float minFallDamageToApply = 10f;

    [Header("交互设置")]
    public Transform[] targetNPCs;
    private const float interactRange = 3f;

    [Header("相机设置")]
    public Transform firstPersonCamera;         // 第一人称相机（拖入你的第一人称相机Transform）
    public FirstPersonCamera externalFirstPersonLook;
    public bool rotateInFirstPerson = true;     // 第一人称时是否允许视角旋转
    /// <summary>
    /// 由 CameraController 控制：为 true 时禁止鼠标视角旋转（镜头过渡期间使用）
    /// </summary>
    public bool disableMouseLook = false;       // ← 从临时版整合，供 CameraController 调用
    private CameraMode currentCameraMode;
    private Transform currentCameraTransform;

    [Header("事件")]
    public UnityEvent OnEnterRange;
    public UnityEvent OnExitRange;
    public UnityEvent<MovementState> OnStateChanged;

    [Header("地图系统")]
    public MapSystem mapSystem;

    [Header("飞行设置")]
    public float flySpeed = 8f;
    public bool canFly = true;
    public bool flyConsumeStamina = true;
    public float flyStaminaConsumeRate = 8f;
    private bool isFlyingUp;
    private bool isFlyingDown;

    private CharacterController characterController;
    private bool isInRange;
    private Vector3 verticalVelocity;
    private MovementState currentState;
    public bool isMovementPaused = false;
    bool _deathReviveFlowActive;
    bool _wasGroundedLastFrame = true;

    public bool IsDeathReviveFlowActive =>
        _deathReviveFlowActive || DeathReviveUI.BlocksGameplayInput;

    void Awake()
    {
        OnEnterRange ??= new UnityEvent();
        OnExitRange ??= new UnityEvent();
        OnStaminaEmpty ??= new UnityEvent();
        OnStaminaRecovered ??= new UnityEvent();
        OnStateChanged ??= new UnityEvent<MovementState>();
        OnStaminaChanged ??= new UnityEvent<float>();
        OnHealthChanged ??= new UnityEvent<float>();
        OnDeath ??= new UnityEvent();

        characterController = GetComponent<CharacterController>();
        currentStamina = maxStamina;
        currentHealth = maxHealth;

        if (respawnPoint == null)
        {
            GameObject respawnObj = new GameObject("RespawnPoint");
            respawnObj.transform.position = transform.position;
            respawnPoint = respawnObj.transform;
        }

        if (firstPersonCamera != null)
        {
            currentCameraTransform = firstPersonCamera;
            currentCameraMode = CameraMode.FirstPerson;
        }
    }

    void Start()
    {
        originalHeight = 1.8f;
        originalCenter = new Vector3(0, originalHeight / 2, 0);
        characterController.height = originalHeight;
        characterController.center = originalCenter;

        if (mapSystem == null)
            mapSystem = FindObjectOfType<MapSystem>();
        if (mapSystem != null && !mapSystem.IsMapFeatureAvailable)
            mapSystem = ResolveMapSystemForInput();

        RefreshFirstPersonCameraBinding();
        OnStaminaChanged.Invoke(GetStaminaRatio());
        OnHealthChanged.Invoke(GetHealthRatio());

        if (characterController != null)
            _wasGroundedLastFrame = characterController.isGrounded;
    }

    void Update()
    {
        HandleMapInput();

        if (isMovementPaused || ChairSitState.BlocksGameplay || IsDeathReviveFlowActive)
            return;

        Vector2 moveAxis = GameInput.GetMovementAxis();
        horizontalInput = moveAxis.x;
        verticalInput = moveAxis.y;

        HandleFlyInput();
        HandleSimpleJump();
        HandleCrouchSmooth();
        SyncFirstPersonCrouchCamera();
        UpdateRunningState();
        UpdateStamina();

        float currentSpeed = CalculateCurrentSpeed();
        Vector3 moveDir = CalculateMoveDirection();
        float vyBeforeMove = verticalVelocity.y;
        Vector3 finalMove = moveDir * currentSpeed * Time.deltaTime + verticalVelocity * Time.deltaTime;
        characterController.Move(finalMove);

        bool groundedNow = characterController.isGrounded;
        if (groundedNow)
        {
            if (enableFallDamage && !_wasGroundedLastFrame && !isFlyingUp && !isFlyingDown)
            {
                float downwardSpeed = Mathf.Max(0f, -vyBeforeMove);
                if (downwardSpeed > safeLandingVerticalSpeed)
                {
                    float excess = downwardSpeed - safeLandingVerticalSpeed;
                    float dmg = Mathf.Min(maxFallDamage, excess * fallDamagePerExcessSpeed);
                    if (dmg >= minFallDamageToApply)
                        TakeDamage(dmg);
                }
            }

            if (verticalVelocity.y < 0f)
                verticalVelocity.y = -2f;
            _wasGroundedLastFrame = true;
        }
        else
        {
            _wasGroundedLastFrame = false;
        }

        if (moveDir.magnitude > 0.1f)
            HandleRotation(moveDir);

        UpdateMovementState(currentSpeed, moveDir);
        CheckRangeStatus();

        // 第一人称视角旋转（有独立 FirstPersonCamera 时由其接管，避免双重旋转）
        if (currentCameraMode == CameraMode.FirstPerson && rotateInFirstPerson && !disableMouseLook
            && (externalFirstPersonLook == null || !externalFirstPersonLook.IsActive))
        {
            HandleFirstPersonLook();
        }
    }

    private void HandleFirstPersonLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * rotationSpeed * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * rotationSpeed * Time.deltaTime;

        transform.Rotate(Vector3.up, mouseX);

        if (currentCameraTransform != null)
        {
            Vector3 currentCamRot = currentCameraTransform.localEulerAngles;
            float newCamRotX = currentCamRot.x - mouseY;
            if (newCamRotX > 180f) newCamRotX -= 360f;
            newCamRotX = Mathf.Clamp(newCamRotX, -80f, 80f);
            currentCameraTransform.localEulerAngles = new Vector3(newCamRotX, 0f, 0f);
        }
    }

    private void HandleFlyInput()
    {
        isFlyingUp = false;
        isFlyingDown = false;

        if (canFly)
        {
            isFlyingUp = Input.GetKey(KeyCode.UpArrow);
            isFlyingDown = Input.GetKey(KeyCode.DownArrow);
        }

        if (isFlyingUp || isFlyingDown)
        {
            verticalVelocity.y = 0;

            float flyVerticalSpeed = 0;
            if (isFlyingUp)
            {
                if (flyConsumeStamina && currentStamina <= 0)
                {
                    flyVerticalSpeed = 0;
                }
                else
                {
                    flyVerticalSpeed = flySpeed;
                    if (flyConsumeStamina)
                    {
                        currentStamina = Mathf.Max(0, currentStamina - flyStaminaConsumeRate * Time.deltaTime);
                        OnStaminaChanged.Invoke(GetStaminaRatio());
                        if (currentStamina <= 0 && canRunAgain)
                        {
                            canRunAgain = false;
                            OnStaminaEmpty.Invoke();
                        }
                    }
                }
            }
            else if (isFlyingDown)
            {
                flyVerticalSpeed = -flySpeed;
            }

            verticalVelocity.y = flyVerticalSpeed;
            return;
        }

        verticalVelocity.y += Physics.gravity.y * gravityScale * Time.deltaTime;
    }

    private void UpdateRunningState()
    {
        bool hasMoveInput = Mathf.Abs(horizontalInput) > 0.1f || Mathf.Abs(verticalInput) > 0.1f;
        isCurrentlyRunning = GameInput.GetKey(GameControlAction.Run)
                          && canRunAgain
                          && currentStamina > 0
                          && !IsCrouching()
                          && hasMoveInput
                          && !isFlyingUp && !isFlyingDown;
    }

    private void HandleSimpleJump()
    {
        if (isFlyingUp || isFlyingDown)
            return;

        if (GameInput.GetKeyDown(GameControlAction.Jump) && characterController.isGrounded && currentStamina >= jumpStaminaCost)
        {
            verticalVelocity.y = jumpForce;
            currentStamina -= jumpStaminaCost;
            OnStaminaChanged.Invoke(GetStaminaRatio());

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                canRunAgain = false;
                OnStaminaEmpty.Invoke();
            }
        }
    }

    private Vector3 CalculateMoveDirection()
    {
        if (currentCameraTransform == null)
            return (transform.forward * verticalInput + transform.right * horizontalInput).normalized;

        Vector3 camForward = Vector3.Scale(currentCameraTransform.forward, new Vector3(1, 0, 1)).normalized;
        Vector3 camRight = Vector3.Scale(currentCameraTransform.right, new Vector3(1, 0, 1)).normalized;
        return (camForward * verticalInput + camRight * horizontalInput).normalized;
    }

    private void HandleRotation(Vector3 moveDir)
    {
        if (currentCameraMode == CameraMode.ThirdPerson)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }
    }

    private void HandleMapInput()
    {
        if (!GameInput.GetKeyDown(GameControlAction.OpenMap))
            return;

        var map = ResolveMapSystemForInput();
        if (map == null)
            return;

        if (map.IsMapWindowOpen)
        {
            map.HideMap();
            isMovementPaused = false;
            Time.timeScale = 1f;
            return;
        }

        GameplayUiHotkeyGuard.CloseOtherExclusivePanels(GameplayUiHotkeyGuard.ExclusivePanelKind.Map);

        isMovementPaused = true;
        Time.timeScale = 0f;
        map.ShowMap();

        if (!map.IsMapWindowOpen)
        {
            isMovementPaused = false;
            Time.timeScale = 1f;
        }
    }

    MapSystem ResolveMapSystemForInput()
    {
        if (mapSystem != null && mapSystem.IsMapFeatureAvailable)
            return mapSystem;

        var scene = gameObject.scene;
        var resolved = MapSystem.FindAvailableInScene(scene);
        mapSystem = resolved;
        return resolved;
    }

    public void SetCameraMode(CameraMode mode, Transform cameraTransform)
    {
        currentCameraMode = mode;
        currentCameraTransform = cameraTransform;

        if (mode == CameraMode.FirstPerson)
        {
            characterController.height = 1.8f;
            characterController.center = new Vector3(0, 0.9f, 0);
        }
        else
        {
            characterController.height = originalHeight;
            characterController.center = originalCenter;
        }
    }

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Max(0, currentHealth - damage);
        OnHealthChanged.Invoke(GetHealthRatio());

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (mapSystem == null)
            mapSystem = FindObjectOfType<MapSystem>();
        if (mapSystem != null)
            mapSystem.CancelOngoingTeleportLoading();

        OnDeath.Invoke();
        Respawn();
    }

    private void Respawn()
    {
        Teleport(respawnPoint.position);
        currentHealth = maxHealth;
        currentStamina = maxStamina;
        OnHealthChanged.Invoke(GetHealthRatio());
        OnStaminaChanged.Invoke(GetStaminaRatio());

        if (currentCameraMode == CameraMode.FirstPerson && currentCameraTransform != null)
        {
            currentCameraTransform.localEulerAngles = Vector3.zero;
            transform.rotation = Quaternion.Euler(0, transform.rotation.y, 0);
        }
    }

    private void HandleCrouchSmooth()
    {
        if (characterController == null) return;
        if (ChairSitState.BlocksGameplay || IsDeathReviveFlowActive)
            return;

        bool wantsCrouch = WantsCrouchInput();
        float standHeight = currentCameraMode == CameraMode.FirstPerson ? 1.8f : originalHeight;
        float targetHeight = wantsCrouch ? crouchHeight : standHeight;

        characterController.height = Mathf.SmoothDamp(
            characterController.height,
            targetHeight,
            ref currentHeightVelocity,
            crouchSmoothTime);
        characterController.center = new Vector3(originalCenter.x, characterController.height / 2, originalCenter.z);
    }

    bool WantsCrouchInput() => GameInput.GetKey(GameControlAction.Crouch);

    void SyncFirstPersonCrouchCamera()
    {
        if (characterController == null)
            return;

        float standHeight = currentCameraMode == CameraMode.FirstPerson ? 1.8f : originalHeight;
        float blend = Mathf.InverseLerp(standHeight, crouchHeight, characterController.height);
        if (externalFirstPersonLook != null)
        {
            externalFirstPersonLook.ApplyCrouchEyeBlend(
                blend,
                standHeight,
                crouchHeight,
                crouchCameraHeightRatio);
        }
    }

    private float CalculateCurrentSpeed()
    {
        float speed = moveSpeed;
        if (isCurrentlyRunning) speed *= runSpeedMultiplier;
        if (IsCrouching()) speed *= crouchSpeedMultiplier;
        return speed;
    }

    private void UpdateStamina()
    {
        if (isCurrentlyRunning || isFlyingUp)
        {
            currentStamina = Mathf.Max(0, currentStamina - (isCurrentlyRunning ? staminaConsumeRate : flyStaminaConsumeRate) * Time.deltaTime);
            OnStaminaChanged.Invoke(GetStaminaRatio());

            if (currentStamina <= 0)
            {
                currentStamina = 0;
                canRunAgain = false;
                OnStaminaEmpty.Invoke();
            }
        }
        else if (currentStamina < maxStamina && !isFlyingUp && !isFlyingDown)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + staminaRecoverRate * Time.deltaTime);
            OnStaminaChanged.Invoke(GetStaminaRatio());

            if (currentStamina >= staminaRunThreshold && !canRunAgain)
            {
                canRunAgain = true;
                OnStaminaRecovered.Invoke();
            }
        }
    }

    private void UpdateMovementState(float speed, Vector3 dir)
    {
        MovementState newState;
        if (IsCrouching())
            newState = MovementState.Crouch;
        else if (dir.magnitude < 0.1f)
            return;
        else if (isCurrentlyRunning)
            newState = MovementState.Run;
        else
            newState = MovementState.Walk;

        if (newState != currentState)
        {
            currentState = newState;
            OnStateChanged.Invoke(newState);
        }
    }

    private void CheckRangeStatus()
    {
        if (targetNPCs == null || targetNPCs.Length == 0) return;

        bool inRange = false;
        foreach (var npc in targetNPCs)
        {
            if (npc != null && Vector3.Distance(transform.position, npc.position) <= interactRange)
            {
                inRange = true;
                break;
            }
        }

        if (inRange && !isInRange)
        {
            isInRange = true;
            OnEnterRange.Invoke();
        }
        else if (!inRange && isInRange)
        {
            isInRange = false;
            OnExitRange.Invoke();
        }
    }

    public bool IsCrouching() =>
        characterController != null && characterController.height < originalHeight - 0.15f;

    public float GetCrouchBlend01()
    {
        if (characterController == null)
            return 0f;
        float standHeight = currentCameraMode == CameraMode.FirstPerson ? 1.8f : originalHeight;
        return Mathf.InverseLerp(standHeight, crouchHeight, characterController.height);
    }
    public float GetStaminaRatio() => currentStamina / maxStamina;
    public float GetHealthRatio() => currentHealth / maxHealth;

    public void Teleport(Vector3 targetPos)
    {
        if (characterController == null) return;
        MapTeleportCameraSnap.BeforePlayerMoves(this);
        characterController.enabled = false;
        transform.position = targetPos;
        characterController.enabled = true;
        verticalVelocity = Vector3.zero;
        _wasGroundedLastFrame = true;
        MapTeleportCameraSnap.AfterPlayerMoved(this, mapSystem != null ? mapSystem.cameraFollow : null);
    }

    public void ApplyWalkingFirstPersonCameraBinding()
    {
        RefreshFirstPersonCameraBinding();
    }

    public void RefreshFirstPersonCameraBinding()
    {
        externalFirstPersonLook = ResolveBoundFirstPersonCamera();
        if (externalFirstPersonLook != null)
            firstPersonCamera = externalFirstPersonLook.transform;
    }

    FirstPersonCamera ResolveBoundFirstPersonCamera()
    {
        var switchers = Object.FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            var sw = switchers[i];
            if (sw == null || sw.firstPersonCamera == null)
                continue;
            if (sw.firstPersonCamera.playerObject == gameObject)
                return sw.firstPersonCamera;
        }

        foreach (var fps in GetComponentsInChildren<FirstPersonCamera>(true))
        {
            if (fps != null && (fps.playerObject == null || fps.playerObject == gameObject))
                return fps;
        }

        return Object.FindObjectOfType<FirstPersonCamera>(true);
    }

    public void ConsumeFood(ProductEntry entry)
    {
        if (entry == null)
            return;

        if (entry.healAmount > 0f)
        {
            currentHealth = Mathf.Min(maxHealth, currentHealth + entry.healAmount);
            OnHealthChanged.Invoke(GetHealthRatio());
        }

        if (entry.staminaRecover > 0f)
        {
            currentStamina = Mathf.Min(maxStamina, currentStamina + entry.staminaRecover);
            OnStaminaChanged.Invoke(GetStaminaRatio());
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);

        if (respawnPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(respawnPoint.position, Vector3.one);
        }
    }
}