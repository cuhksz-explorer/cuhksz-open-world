using UnityEngine;

/// <summary>
/// ÃÃ Â»ÃºÂ¸ÃºÃ‹Ã¦Ã—Ã©Â¼Ã¾Â£Â¨Ã“Ã…Â»Â¯Â´Â«Ã‹ÃÃ‹Â²Ã’Ã†Ã‚ÃŸÂ¼Â­Â£Â©
/// Ã„Â¿Â±ÃªÃ‹Â²Ã’Ã†ÃŠÂ±ÃÃ Â»ÃºÃ–Â±Â½Ã“Â¶Â¨ÃŽÂ»Â£Â¬Ã•Ã½Â³Â£Ã’Ã†Â¶Â¯ÃŠÂ±Ã†Â½Â»Â¬Â¸ÃºÃ‹Ã¦
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Â»Ã¹Â´Â¡Â¸ÃºÃ‹Ã¦Ã‰Ã¨Ã–Ãƒ")]
    [Tooltip("Ã„Â¬ÃˆÃÂ¸ÃºÃ‹Ã¦Ã„Â¿Â±ÃªÂ£Â¨Â¿Ã‰Ã”Ãš Inspector Ã–Â±Â½Ã“Ã–Â¸Â¶Â¨Â£Â©")]
    public Transform defaultTarget;
    [Tooltip("ÃÃ Â»ÃºÃ“Ã«Ã„Â¿Â±ÃªÂµÃ„Ã†Â«Ã’Ã†ÃÂ¿Â£Â¨ÃŠÃ€Â½Ã§Ã—Ã¸Â±ÃªÂ£Â©")]
    public Vector3 followOffset = new Vector3(0, 5, -10);
    [Tooltip("Â¸ÃºÃ‹Ã¦Ã†Â½Â»Â¬Ã‹Ã™Â¶ÃˆÂ£Â¨Ã–ÂµÃ”Â½Â´Ã³Ã”Â½ÃÃ©ÃƒÃ´Â£Â¬Â½Â¨Ã’Ã© 0.1-1 Ã–Â®Â¼Ã¤Â£Â©")]
    public float smoothSpeed = 0.2f;

    [Header("Ã‹Â²Ã’Ã†Â¼Ã¬Â²Ã¢Ã‰Ã¨Ã–ÃƒÂ£Â¨Â¹Ã˜Â¼Ã¼Ã“Ã…Â»Â¯Â£Â©")]
    [Tooltip("Ã„Â¿Â±ÃªÃŽÂ»Ã–ÃƒÃÂ»Â±Ã¤Ã£ÃÃ–ÂµÂ£Â¨Â³Â¬Â¹Ã½Â´Ã‹Â¾Ã Ã€Ã«Ã…ÃÂ¶Â¨ÃŽÂªÃ‹Â²Ã’Ã†Â£Â¬ÂµÂ¥ÃŽÂ»Â£ÂºÃƒÃ—Â£Â©")]
    public float teleportThreshold = 10f; // Â¿Ã‰Â¸Ã¹Â¾ÃÂ³Â¡Â¾Â°Â´Ã³ÃÂ¡ÂµÃ·Ã•Ã»Â£Â¬Ã„Â¬ÃˆÃ10ÃƒÃ—
    [Tooltip("Ã‹Â²Ã’Ã†ÃŠÂ±ÃÃ Â»ÃºÃŠÃ‡Â·Ã±Ã–Â±Â½Ã“Â¶Â¨ÃŽÂ»Â£Â¨Â¹Â´Ã‘Â¡=Ã–Â±Â½Ã“Ã‹Â²Ã’Ã†Â£Â¬ÃˆÂ¡ÃÃ»=Â±Â£ÃÃ´Ã†Â½Â»Â¬Â£Â©")]
    public bool teleportCameraInstantly = true;

    [Header("Ã–Ã¡Ã‹Ã¸Â¶Â¨Ã‰Ã¨Ã–Ãƒ")]
    [Tooltip("ÃŠÃ‡Â·Ã±Ã‹Ã¸Â¶Â¨XÃ–Ã¡Â£Â¨ÃÃ Â»ÃºXÃ—Ã¸Â±ÃªÂ²Â»Â¸ÃºÃ‹Ã¦Ã„Â¿Â±ÃªÂ£Â©")]
    public bool lockXAxis = false;
    [Tooltip("ÃŠÃ‡Â·Ã±Ã‹Ã¸Â¶Â¨YÃ–Ã¡Â£Â¨ÃÃ Â»ÃºYÃ—Ã¸Â±ÃªÂ²Â»Â¸ÃºÃ‹Ã¦Ã„Â¿Â±ÃªÂ£Â©")]
    public bool lockYAxis = false;
    [Tooltip("ÃŠÃ‡Â·Ã±Ã‹Ã¸Â¶Â¨ZÃ–Ã¡Â£Â¨ÃÃ Â»ÃºZÃ—Ã¸Â±ÃªÂ²Â»Â¸ÃºÃ‹Ã¦Ã„Â¿Â±ÃªÂ£Â©")]
    public bool lockZAxis = false;

    [Header("Â¸ÃºÃ‹Ã¦Â·Â¶ÃŽÂ§ÃÃžÃ–Ã†Â£Â¨Â¿Ã‰Ã‘Â¡Â£Â©")]
    [Tooltip("ÃŠÃ‡Â·Ã±Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†")]
    public bool enableRangeLimit = false;
    [Tooltip("ÃÃ Â»ÃºÃ—Ã®ÃÂ¡XÃ—Ã¸Â±ÃªÂ£Â¨Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†ÃŠÂ±Ã‰ÃºÃÂ§Â£Â©")]
    public float minX = -50f;
    [Tooltip("ÃÃ Â»ÃºÃ—Ã®Â´Ã³XÃ—Ã¸Â±ÃªÂ£Â¨Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†ÃŠÂ±Ã‰ÃºÃÂ§Â£Â©")]
    public float maxX = 50f;
    [Tooltip("ÃÃ Â»ÃºÃ—Ã®ÃÂ¡YÃ—Ã¸Â±ÃªÂ£Â¨Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†ÃŠÂ±Ã‰ÃºÃÂ§Â£Â©")]
    public float minY = 2f;
    [Tooltip("ÃÃ Â»ÃºÃ—Ã®Â´Ã³YÃ—Ã¸Â±ÃªÂ£Â¨Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†ÃŠÂ±Ã‰ÃºÃÂ§Â£Â©")]
    public float maxY = 15f;
    [Tooltip("ÃÃ Â»ÃºÃ—Ã®ÃÂ¡ZÃ—Ã¸Â±ÃªÂ£Â¨Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†ÃŠÂ±Ã‰ÃºÃÂ§Â£Â©")]
    public float minZ = -50f;
    [Tooltip("ÃÃ Â»ÃºÃ—Ã®Â´Ã³ZÃ—Ã¸Â±ÃªÂ£Â¨Ã†Ã´Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†ÃŠÂ±Ã‰ÃºÃÂ§Â£Â©")]
    public float maxZ = 0f;

    [Header("Ã„Â¿Â±ÃªÂ¶ÂªÃŠÂ§Â´Â¦Ã€Ã­")]
    [Tooltip("Ã„Â¿Â±ÃªÂ¶ÂªÃŠÂ§ÂºÃ³ÃŠÃ‡Â·Ã±Ã—Ã”Â¶Â¯Â²Ã©Ã•Ã’Ã„Â¬ÃˆÃÃ„Â¿Â±Ãª")]
    public bool autoFindDefaultTarget = true;
    [Tooltip("Ã„Â¿Â±ÃªÂ¶ÂªÃŠÂ§ÂºÃ³ÂµÃ„ÂµÃˆÂ´Ã½ÃŠÂ±Â¼Ã¤Â£Â¨ÃƒÃ«Â£Â©")]
    public float targetLostWaitTime = 2f;

    private Transform currentTarget; // ÂµÂ±Ã‡Â°Â¸ÃºÃ‹Ã¦Ã„Â¿Â±Ãª
    private Vector3 lastTargetPosition; // Ã‰ÃÃ’Â»Ã–Â¡Ã„Â¿Â±ÃªÃŽÂ»Ã–ÃƒÂ£Â¨Ã“ÃƒÃ“ÃšÂ¼Ã¬Â²Ã¢Ã‹Â²Ã’Ã†Â£Â©
    private float targetLostTimer; // Ã„Â¿Â±ÃªÂ¶ÂªÃŠÂ§Â¼Ã†ÃŠÂ±Ã†Ã·

    private void Start()
    {
        // Â³ÃµÃŠÂ¼Â»Â¯Â¸ÃºÃ‹Ã¦Ã„Â¿Â±Ãª
        if (defaultTarget != null)
        {
            currentTarget = defaultTarget;
            lastTargetPosition = currentTarget.position; // Â¼Ã‡Ã‚Â¼Â³ÃµÃŠÂ¼ÃŽÂ»Ã–Ãƒ
            Debug.Log($"CameraFollowÂ£ÂºÂ³ÃµÃŠÂ¼Â¸ÃºÃ‹Ã¦Ã„Â¿Â±Ãª -> {defaultTarget.gameObject.name}");
        }
        else if (autoFindDefaultTarget)
        {
            // Ã—Ã”Â¶Â¯Â²Ã©Ã•Ã’ÃÃ¦Â¼Ã’Â£Â¨Â±ÃªÃ‡Â©ÃŽÂª "Player" ÂµÃ„ÃŽÃ¯ÃŒÃ¥Â£Â©
            FindPlayerTarget();
        }

        // Â³ÃµÃŠÂ¼Â»Â¯ÃÃ Â»ÃºÃŽÂ»Ã–ÃƒÂ£Â¨ÃˆÃ§Â¹Ã»Ã“ÃÃ„Â¿Â±ÃªÂ£Â¬Ã–Â±Â½Ã“Â¶Â¨ÃŽÂ»ÂµÂ½Ã„Â¿Â±ÃªÂ¶Ã”Ã“Â¦ÂµÃ„ÃÃ Â»ÃºÃŽÂ»Ã–ÃƒÂ£Â©
        if (currentTarget != null)
        {
            Vector3 initialCameraPos = CalculateTargetCameraPosition(currentTarget.position);
            transform.position = initialCameraPos;
            lastTargetPosition = currentTarget.position;
        }
    }

    private void LateUpdate()
    {
        // Â´Â¦Ã€Ã­Ã„Â¿Â±ÃªÂ¸ÃºÃ‹Ã¦Ã‚ÃŸÂ¼Â­Â£Â¨LateUpdate ÃˆÂ·Â±Â£Ã”ÃšÃÃ¦Â¼Ã’Ã’Ã†Â¶Â¯ÂºÃ³Ã–Â´ÃÃÂ£Â¬Â±ÃœÃƒÃ¢ÃÃ Â»ÃºÂ¶Â¶Â¶Â¯Â£Â©
        if (BlocksFollowBecauseFirstPersonActive())
            return;

        HandleTargetFollow();
    }

    bool BlocksFollowBecauseFirstPersonActive()
    {
        var fps = GetComponent<FirstPersonCamera>();
        if (fps != null && (fps.IsActive || fps.IsRendering))
            return true;

        var switchers = FindObjectsOfType<CameraSwitcher>(true);
        for (int i = 0; i < switchers.Length; i++)
        {
            if (switchers[i] != null && switchers[i].IsFirstPersonActive)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Â´Â¦Ã€Ã­ÃÃ Â»ÃºÂ¸ÃºÃ‹Ã¦Ã‚ÃŸÂ¼Â­Â£Â¨ÂºÃ‹ÃÃ„Ã“Ã…Â»Â¯Â£ÂºÂ¼Ã¬Â²Ã¢Ã‹Â²Ã’Ã†Â£Â©
    /// </summary>
    private void HandleTargetFollow()
    {
        if (BlocksFollowBecauseFirstPersonActive())
            return;

        if (currentTarget != null)
        {
            targetLostTimer = 0f; // Ã–Ã˜Ã–ÃƒÂ¶ÂªÃŠÂ§Â¼Ã†ÃŠÂ±Ã†Ã·

            // Â¼Ã†Ã‹Ã£Ã„Â¿Â±ÃªÂµÂ±Ã‡Â°Ã“Â¦Â¶Ã”Ã“Â¦ÂµÃ„ÃÃ Â»ÃºÃŽÂ»Ã–Ãƒ
            Vector3 targetCameraPos = CalculateTargetCameraPosition(currentTarget.position);

            // Â¼Ã¬Â²Ã¢Ã„Â¿Â±ÃªÃŠÃ‡Â·Ã±Ã‹Â²Ã’Ã†Â£Â¨ÂµÂ±Ã‡Â°ÃŽÂ»Ã–ÃƒÃ“Ã«Ã‰ÃÃ’Â»Ã–Â¡ÃŽÂ»Ã–ÃƒÂ¾Ã Ã€Ã«Â³Â¬Â¹Ã½Ã£ÃÃ–ÂµÂ£Â©
            bool isTargetTeleported = Vector3.Distance(currentTarget.position, lastTargetPosition) > teleportThreshold;

            // Â¸Ã¹Â¾ÃÃŠÃ‡Â·Ã±Ã‹Â²Ã’Ã†Â£Â¬Ã‘Â¡Ã”Ã±ÃÃ Â»ÃºÃ’Ã†Â¶Â¯Â·Â½ÃŠÂ½
            if (isTargetTeleported && teleportCameraInstantly)
            {
                // Ã„Â¿Â±ÃªÃ‹Â²Ã’Ã†Â£ÂºÃÃ Â»ÃºÃ–Â±Â½Ã“Â¶Â¨ÃŽÂ»ÂµÂ½Ã„Â¿Â±ÃªÃŽÂ»Ã–ÃƒÂ£Â¨ÃŽÃžÃ†Â½Â»Â¬Â£Â©
                transform.position = targetCameraPos;
                Debug.Log($"CameraFollowÂ£ÂºÂ¼Ã¬Â²Ã¢ÂµÂ½Ã„Â¿Â±ÃªÃ‹Â²Ã’Ã†Â£Â¬ÃÃ Â»ÃºÃ–Â±Â½Ã“Â¶Â¨ÃŽÂ»ÂµÂ½ÃÃ‚ÃŽÂ»Ã–Ãƒ");
            }
            else
            {
                // Ã„Â¿Â±ÃªÃ•Ã½Â³Â£Ã’Ã†Â¶Â¯Â£ÂºÃ†Â½Â»Â¬Â¸ÃºÃ‹Ã¦
                Vector3 smoothedPosition = Vector3.Lerp(transform.position, targetCameraPos, smoothSpeed);
                transform.position = smoothedPosition;
            }

            // Â¸Ã¼ÃÃ‚Ã‰ÃÃ’Â»Ã–Â¡Ã„Â¿Â±ÃªÃŽÂ»Ã–ÃƒÂ£Â¨Ã“ÃƒÃ“ÃšÃÃ‚Â´ÃŽÂ¼Ã¬Â²Ã¢Â£Â©
            lastTargetPosition = currentTarget.position;
        }
        else
        {
            // Ã„Â¿Â±ÃªÂ¶ÂªÃŠÂ§ÃŠÂ±ÂµÃ„Â´Â¦Ã€Ã­
            HandleTargetLost();
        }
    }

    /// <summary>
    /// Â¼Ã†Ã‹Ã£Ã„Â¿Â±ÃªÂ¶Ã”Ã“Â¦ÂµÃ„ÃÃ Â»ÃºÃŽÂ»Ã–ÃƒÂ£Â¨Ã“Â¦Ã“ÃƒÃ†Â«Ã’Ã†Â¡Â¢Ã–Ã¡Ã‹Ã¸Â¶Â¨Â¡Â¢Â·Â¶ÃŽÂ§ÃÃžÃ–Ã†Â£Â©
    /// </summary>
    private Vector3 CalculateTargetCameraPosition(Vector3 targetWorldPos)
    {
        // Â»Ã¹Â´Â¡ÃŽÂ»Ã–Ãƒ = Ã„Â¿Â±ÃªÃŠÃ€Â½Ã§ÃŽÂ»Ã–Ãƒ + ÃÃ Â»ÃºÃ†Â«Ã’Ã†
        Vector3 cameraPos = targetWorldPos + followOffset;

        // Ã“Â¦Ã“ÃƒÃ–Ã¡Ã‹Ã¸Â¶Â¨Â£Â¨Â¹Ã½Ã‚Ã‹Â²Â»ÃÃ¨Ã’ÂªÂ¸ÃºÃ‹Ã¦ÂµÃ„Ã–Ã¡Â£Â©
        if (lockXAxis) cameraPos.x = transform.position.x;
        if (lockYAxis) cameraPos.y = transform.position.y;
        if (lockZAxis) cameraPos.z = transform.position.z;

        // Ã“Â¦Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†
        if (enableRangeLimit)
        {
            cameraPos.x = Mathf.Clamp(cameraPos.x, minX, maxX);
            cameraPos.y = Mathf.Clamp(cameraPos.y, minY, maxY);
            cameraPos.z = Mathf.Clamp(cameraPos.z, minZ, maxZ);
        }

        return cameraPos;
    }

    /// <summary>
    /// Ã„Â¿Â±ÃªÂ¶ÂªÃŠÂ§ÂºÃ³ÂµÃ„Â´Â¦Ã€Ã­Ã‚ÃŸÂ¼Â­
    /// </summary>
    private void HandleTargetLost()
    {
        targetLostTimer += Time.deltaTime;

        // Â³Â¬Â¹Ã½ÂµÃˆÂ´Ã½ÃŠÂ±Â¼Ã¤ÂºÃ³Â³Â¢ÃŠÃ”Ã—Ã”Â¶Â¯Â²Ã©Ã•Ã’Ã„Â¿Â±Ãª
        if (autoFindDefaultTarget && targetLostTimer >= targetLostWaitTime)
        {
            FindPlayerTarget();
            targetLostTimer = 0f;
        }
    }

    /// <summary>
    /// Ã—Ã”Â¶Â¯Â²Ã©Ã•Ã’Â±ÃªÃ‡Â©ÃŽÂª "Player" ÂµÃ„Ã„Â¿Â±Ãª
    /// </summary>
    private void FindPlayerTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            currentTarget = player.transform;
            lastTargetPosition = currentTarget.position; // Â³ÃµÃŠÂ¼Â»Â¯Ã‰ÃÃ’Â»Ã–Â¡ÃŽÂ»Ã–Ãƒ
            // Ã—Ã”Â¶Â¯Â¶Â¨ÃŽÂ»ÂµÂ½ÃÃ¦Â¼Ã’Â¶Ã”Ã“Â¦ÂµÃ„ÃÃ Â»ÃºÃŽÂ»Ã–Ãƒ
            transform.position = CalculateTargetCameraPosition(currentTarget.position);
            Debug.Log($"CameraFollowÂ£ÂºÃ—Ã”Â¶Â¯Ã•Ã’ÂµÂ½ÃÃ¦Â¼Ã’Ã„Â¿Â±Ãª -> {player.name}Â£Â¬ÃÃ Â»ÃºÃ’Ã‘Â¶Â¨ÃŽÂ»");
        }
        else
        {
            Debug.LogWarning("CameraFollowÂ£ÂºÃŽÂ´Ã•Ã’ÂµÂ½Â±ÃªÃ‡Â©ÃŽÂª 'Player' ÂµÃ„Ã„Â¿Â±ÃªÂ£Â¬Ã‡Ã«Â¼Ã¬Â²Ã©ÃÃ¦Â¼Ã’Â±ÃªÃ‡Â©Â»Ã²ÃŠÃ–Â¶Â¯Ã–Â¸Â¶Â¨ defaultTarget");
        }
    }

    /// <summary>
    /// Ã‰Ã¨Ã–ÃƒÂ¸ÃºÃ‹Ã¦Ã„Â¿Â±ÃªÂ£Â¨Â¼Ã¦ÃˆÃ MapSystem ÂµÃ„ÂµÃ·Ã“ÃƒÂ£Â©
    /// </summary>
    /// <param name="newTarget">ÃÃ‚ÂµÃ„Â¸ÃºÃ‹Ã¦Ã„Â¿Â±Ãª</param>
    public void SetTarget(Transform newTarget)
    {
        if (newTarget != null)
        {
            currentTarget = newTarget;
            lastTargetPosition = currentTarget.position;
            if (BlocksFollowBecauseFirstPersonActive())
                return;

            Vector3 newCameraPos = CalculateTargetCameraPosition(currentTarget.position);
            transform.position = newCameraPos;
            Debug.Log($"CameraFollowÂ£ÂºÂ¸ÃºÃ‹Ã¦Ã„Â¿Â±ÃªÃ’Ã‘Â¸Ã¼ÃÃ‚ÃŽÂª -> {newTarget.gameObject.name}Â£Â¬ÃÃ Â»ÃºÃ–Â±Â½Ã“Â¶Â¨ÃŽÂ»");
        }
        else
        {
            Debug.LogWarning("CameraFollowÂ£ÂºÂ³Â¢ÃŠÃ”Ã‰Ã¨Ã–ÃƒÂ¿Ã•Ã„Â¿Â±ÃªÂ£Â¬ÂºÃ¶Ã‚Ã”Â¸ÃƒÂ²Ã™Ã—Ã·");
        }
    }

    public void NotifyTargetTeleported(Transform newTarget)
    {
        if (newTarget == null)
            return;

        currentTarget = newTarget;
        lastTargetPosition = newTarget.position;
        if (BlocksFollowBecauseFirstPersonActive())
            return;

        transform.position = CalculateTargetCameraPosition(newTarget.position);
    }

    /// <summary>
    /// Â¶Â¯ÃŒÂ¬ÃÃžÂ¸Ã„ÃÃ Â»ÃºÃ†Â«Ã’Ã†ÃÂ¿Â£Â¨Ã–Â§Â³Ã–Â´ÃºÃ‚Ã«ÂµÃ·Ã“ÃƒÂ£Â©
    /// </summary>
    /// <param name="newOffset">ÃÃ‚ÂµÃ„Ã†Â«Ã’Ã†ÃÂ¿</param>
    public void SetFollowOffset(Vector3 newOffset)
    {
        followOffset = newOffset;
        Debug.Log($"CameraFollowÂ£ÂºÃÃ Â»ÃºÃ†Â«Ã’Ã†ÃÂ¿Ã’Ã‘Â¸Ã¼ÃÃ‚ÃŽÂª -> {newOffset}");
    }

    /// <summary>
    /// Â¶Â¯ÃŒÂ¬ÃÃžÂ¸Ã„Ã†Â½Â»Â¬Ã‹Ã™Â¶ÃˆÂ£Â¨Ã–Â§Â³Ã–Â´ÃºÃ‚Ã«ÂµÃ·Ã“ÃƒÂ£Â©
    /// </summary>
    /// <param name="newSmoothSpeed">ÃÃ‚ÂµÃ„Ã†Â½Â»Â¬Ã‹Ã™Â¶Ãˆ</param>
    public void SetSmoothSpeed(float newSmoothSpeed)
    {
        smoothSpeed = Mathf.Clamp(newSmoothSpeed, 0.01f, 5f); // ÃÃžÃ–Ã†Ã‹Ã™Â¶ÃˆÂ·Â¶ÃŽÂ§Â£Â¬Â±ÃœÃƒÃ¢Ã’Ã¬Â³Â£
        Debug.Log($"CameraFollowÂ£ÂºÃ†Â½Â»Â¬Ã‹Ã™Â¶ÃˆÃ’Ã‘Â¸Ã¼ÃÃ‚ÃŽÂª -> {smoothSpeed}");
    }

    /// <summary>
    /// Ã†Ã´Ã“Ãƒ/Â½Ã»Ã“ÃƒÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†Â£Â¨Ã–Â§Â³Ã–Â´ÃºÃ‚Ã«ÂµÃ·Ã“ÃƒÂ£Â©
    /// </summary>
    public void ToggleRangeLimit(bool isEnabled)
    {
        enableRangeLimit = isEnabled;
        Debug.Log($"CameraFollowÂ£ÂºÂ·Â¶ÃŽÂ§ÃÃžÃ–Ã†Ã’Ã‘{(isEnabled ? "Ã†Ã´Ã“Ãƒ" : "Â½Ã»Ã“Ãƒ")}");
    }
}