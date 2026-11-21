using UnityEngine;

[RequireComponent(typeof(AudioSource))]
[DefaultExecutionOrder(100)]
public class PlayerStepSound : MonoBehaviour
{
    [Header("Footsteps")]
    public AudioClip walkClip;
    [Range(0.1f, 1f)]
    public float stepVolume = 0.7f;
    [Range(0.8f, 1.2f)]
    public float walkPitch = 1f;
    [Range(0.9f, 1.2f)]
    public float crouchPitch = 0.8f;

    PlayerMovement playerMovement;
    AudioSource stepAudioSource;
    MovementState currentMovementState = MovementState.Walk;

    void Awake()
    {
        var sources = GetComponents<AudioSource>();
        stepAudioSource = sources.Length > 0 ? sources[0] : gameObject.AddComponent<AudioSource>();

        playerMovement = GetComponent<PlayerMovement>();
        if (playerMovement == null)
        {
            Debug.LogError("[PlayerStepSound] PlayerMovement not found on same GameObject", gameObject);
            enabled = false;
            return;
        }

        ConfigureStepAudioSource();
        StopExtraPantSources();

        playerMovement.OnStateChanged.AddListener(OnMovementStateChanged);
    }

    void StopExtraPantSources()
    {
        var sources = GetComponents<AudioSource>();
        for (int i = 1; i < sources.Length; i++)
        {
            if (sources[i] == null)
                continue;

            sources[i].playOnAwake = false;
            sources[i].Stop();
            sources[i].clip = null;
        }
    }

    void ConfigureStepAudioSource()
    {
        stepAudioSource.loop = true;
        stepAudioSource.volume = stepVolume;
        stepAudioSource.spatialBlend = 1f;
        stepAudioSource.maxDistance = 15f;
        stepAudioSource.mute = false;
        stepAudioSource.playOnAwake = false;
        stepAudioSource.clip = walkClip;
        stepAudioSource.pitch = walkPitch;
    }

    void Update()
    {
        if (playerMovement.isMovementPaused)
        {
            StopAllSound();
            return;
        }

        bool isMoving = Mathf.Abs(playerMovement.horizontalInput) > 0.1f ||
                        Mathf.Abs(playerMovement.verticalInput) > 0.1f;
        UpdateStepSound(isMoving);
    }

    void UpdateStepSound(bool isMoving)
    {
        if (currentMovementState == MovementState.Run)
        {
            if (stepAudioSource.isPlaying)
                stepAudioSource.Stop();
            return;
        }

        if (stepAudioSource.clip == null && walkClip != null)
        {
            stepAudioSource.clip = walkClip;
            stepAudioSource.pitch = walkPitch;
            stepAudioSource.volume = stepVolume;
        }

        if (isMoving && !stepAudioSource.isPlaying)
            stepAudioSource.Play();
        else if (!isMoving && stepAudioSource.isPlaying)
            stepAudioSource.Stop();
    }

    void OnMovementStateChanged(MovementState newState)
    {
        currentMovementState = newState;

        switch (newState)
        {
            case MovementState.Walk:
                stepAudioSource.clip = walkClip;
                stepAudioSource.pitch = walkPitch;
                stepAudioSource.volume = stepVolume;
                break;
            case MovementState.Run:
                stepAudioSource.Stop();
                break;
            case MovementState.Crouch:
                stepAudioSource.clip = walkClip;
                stepAudioSource.pitch = walkPitch * crouchPitch;
                stepAudioSource.volume = stepVolume;
                break;
        }
    }

    public void ResetStepSoundState()
    {
        stepAudioSource.Stop();
        stepAudioSource.clip = walkClip;
        stepAudioSource.pitch = walkPitch;
        stepAudioSource.volume = stepVolume;
        currentMovementState = MovementState.Walk;
    }

    void StopAllSound()
    {
        if (stepAudioSource != null && stepAudioSource.isPlaying)
            stepAudioSource.Stop();
    }

    void OnDestroy()
    {
        if (playerMovement == null)
            return;

        playerMovement.OnStateChanged.RemoveListener(OnMovementStateChanged);
    }
}
