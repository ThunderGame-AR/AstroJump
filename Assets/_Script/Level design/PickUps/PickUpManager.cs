using UnityEngine;
using TMPro;

public class PickUpManager : MonoBehaviour
{
    [Header("Global Pickup Animation")]
    [Min(0f)] public float rotationSpeed = 100f;
    [Min(0f)] public float bobbingSpeed = 2f;
    [Min(0f)] public float bobbingAmount = 0.5f;

    [Header("UI Panels")]
    public GameObject shieldPanel;
    public GameObject speedPanel;
    public GameObject jumpPanel;

    [Header("UI Texts")]
    public TextMeshProUGUI shieldTimerText;
    public TextMeshProUGUI speedTimerText;
    public TextMeshProUGUI jumpTimerText;

    [Header("References")]
    [SerializeField] private PersonController personController;
    [SerializeField] private GamePause gamePauseScript;
    [SerializeField] private EndGame endGameScript;

    [Header("Visual Effects")]
    public GameObject shieldVisual;
    public ParticleSystem speedAuraParticles;
    public ParticleSystem jumpAuraParticles;

    [Header("SpawnPoint Feedback")]
    public GameObject spawnPointPin;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spawnPointSound;
    [SerializeField] private AudioClip collectedSound;
    [SerializeField] private AudioSource shieldLoopSource;
    [SerializeField] private AudioClip destructionSound;

    public static PickUpManager Instance;

    private float _shieldTime, _speedTime, _jumpTime;
    private float _shieldRedThreshold, _speedRedThreshold, _jumpRedThreshold;

    private float _currentSpeedMultiplier = 1f;
    private float _currentJumpMultiplier = 1f;

    private bool _keepShieldOnRespawn, _keepSpeedOnRespawn, _keepJumpOnRespawn;
    private bool _canPlayCollectSound = false;

    private Vector3 _lastPlayerPosition;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetUIState(shieldPanel, false);
        SetUIState(speedPanel, false);
        SetUIState(jumpPanel, false);

        if (shieldVisual != null) shieldVisual.SetActive(false);
        if (speedAuraParticles != null) speedAuraParticles.Stop();
        if (jumpAuraParticles != null) jumpAuraParticles.Stop();
        if (spawnPointPin != null) spawnPointPin.SetActive(false);

        _lastPlayerPosition = personController.transform.position;

        Invoke(nameof(EnableCollectSound), 0.5f);
    }

    private void EnableCollectSound() => _canPlayCollectSound = true;

    public void PlayCollectSound(bool isCheckpoint)
    {
        if (audioSource == null || !_canPlayCollectSound) return;
        audioSource.PlayOneShot(isCheckpoint ? spawnPointSound : collectedSound);
    }

    public void PlayDestructionSound()
    {
        if (audioSource != null && destructionSound != null && _canPlayCollectSound)
            audioSource.PlayOneShot(destructionSound);
    }

    public void ActivateShield(float duration, float redThreshold, bool keepOnRespawn)
    {
        _shieldTime = duration;
        _shieldRedThreshold = redThreshold;
        _keepShieldOnRespawn = keepOnRespawn;
        SetUIState(shieldPanel, true);
        if (shieldVisual != null) shieldVisual.SetActive(true);
    }

    public void ActivateSpeed(float duration, float redThreshold, float multiplier, bool keepOnRespawn)
    {
        _speedTime = duration;
        _speedRedThreshold = redThreshold;
        _currentSpeedMultiplier = multiplier;
        _keepSpeedOnRespawn = keepOnRespawn;
        SetUIState(speedPanel, true);

        if (speedAuraParticles != null)
        {
            speedAuraParticles.Clear();
            if (!speedAuraParticles.isPlaying) speedAuraParticles.Play();
        }
    }

    public void ActivateJump(float duration, float redThreshold, float multiplier, bool keepOnRespawn)
    {
        _jumpTime = duration;
        _jumpRedThreshold = redThreshold;
        _currentJumpMultiplier = multiplier;
        _keepJumpOnRespawn = keepOnRespawn;
        SetUIState(jumpPanel, true);

        if (jumpAuraParticles != null)
        {
            jumpAuraParticles.Clear();
            if (!jumpAuraParticles.isPlaying) jumpAuraParticles.Play();
        }
    }

    public void SetNewCheckpoint(Vector3 position)
    {
        if (spawnPointPin != null)
        {
            spawnPointPin.SetActive(true);
            spawnPointPin.transform.position = position + new Vector3(0, 2f, 0);

            ParticleSystem[] psList = spawnPointPin.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in psList)
            {
                ps.Clear();
                ps.Play();
            }
        }
    }

    private void Update()
    {
        if (spawnPointPin != null && spawnPointPin.activeSelf)
        {
            ParticleSystem[] cpParticles = spawnPointPin.GetComponentsInChildren<ParticleSystem>();
            foreach (var ps in cpParticles)
            {
                HandleParticlePause(ps, true);
            }
            spawnPointPin.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }

        HandleParticlePause(speedAuraParticles, IsSpeedActive());
        HandleParticlePause(jumpAuraParticles, IsJumpActive());

        if ((endGameScript != null && endGameScript.isGameOver) || (gamePauseScript != null && gamePauseScript.paused))
            return;

        HandleTimer(ref _shieldTime, _shieldRedThreshold, shieldPanel, shieldTimerText, PowerUpType.Shield);
        HandleTimer(ref _speedTime, _speedRedThreshold, speedPanel, speedTimerText, PowerUpType.Speed);
        HandleTimer(ref _jumpTime, _jumpRedThreshold, jumpPanel, jumpTimerText, PowerUpType.Jump);

        HandleSpeedAuraLogic();

        HandleJumpAuraLogic();

        HandleShieldAudioLoop();
    }

    private void HandleShieldAudioLoop()
    {
        if (shieldLoopSource == null) return;

        bool shieldShouldBeActive = IsShieldActive() || (shieldVisual != null && shieldVisual.activeSelf);

        if (shieldShouldBeActive)
        {
            if (!shieldLoopSource.isPlaying)
            {
                shieldLoopSource.loop = true;
                shieldLoopSource.Play();
            }
        }
        else
        {
            if (shieldLoopSource.isPlaying)
            {
                shieldLoopSource.Stop();
            }
        }
    }

    private enum PowerUpType { Shield, Speed, Jump }

    private void HandleTimer(ref float time, float threshold, GameObject panel, TextMeshProUGUI text, PowerUpType type)
    {
        if (time > 0)
        {
            time -= Time.deltaTime;
            text.text = "-" + FormatTime(time + 1);

            text.color = (time <= threshold) ? Color.red : Color.white;

            if (time <= 0)
            {
                SetUIState(panel, false);
                PlayDestructionSound();
                ResetPowerUpEffect(type);
            }
        }
    }

    private void ResetPowerUpEffect(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Shield:
                bool isInShieldArea = personController.currentActiveArea != null && personController.currentActiveArea.IsAreaActive() && personController.currentActiveArea.givesShieldInTheArea;
                if (shieldVisual != null && !isInShieldArea && !IsShieldActive()) shieldVisual.SetActive(false);
                break;
            case PowerUpType.Speed:
                if (speedAuraParticles != null)
                    speedAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                _currentSpeedMultiplier = 1f;
                break;
            case PowerUpType.Jump:
                if (jumpAuraParticles != null)
                    jumpAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _currentJumpMultiplier = 1f;
                break;
        }
    }

    private void HandleSpeedAuraLogic()
    {
        if (speedAuraParticles == null || !IsSpeedActive() || personController == null)
            return;

        var mainModule = speedAuraParticles.main;
        mainModule.simulationSpace = ParticleSystemSimulationSpace.World;

        var inheritVelocity = speedAuraParticles.inheritVelocity;
        Vector3 velocity = personController.GetComponent<CharacterController>().velocity;
        float horizontalSpeed = new Vector2(velocity.x, velocity.z).magnitude;

        if (horizontalSpeed < 0.5f)
        {
            inheritVelocity.curve = 1.0f;
        }
        else
        {
            inheritVelocity.curve = 0.2f;
        }
    }

    private void HandleJumpAuraLogic()
    {
        if (jumpAuraParticles == null || !IsJumpActive()) return;

        var inheritModule = jumpAuraParticles.inheritVelocity;

        inheritModule.enabled = true;

        if (personController.isGrounded)
        {
            inheritModule.curve = 1.0f;
        }
        else
        {
            inheritModule.curve = 0.0f;
        }
    }

    private void HandleParticlePause(ParticleSystem ps, bool isActive)
    {
        if (ps == null || !isActive) return;
        bool isGamePaused = (endGameScript != null && endGameScript.isGameOver) || (gamePauseScript != null && gamePauseScript.paused);

        if (isGamePaused && !ps.isPaused) ps.Pause();
        else if (!isGamePaused && ps.isPaused) ps.Play();
    }

    public void ResetPickUpsOnRespawn()
    {
        if (!_keepShieldOnRespawn)
        {
            if (IsShieldActive()) PlayDestructionSound();
            _shieldTime = 0;
            ResetPowerUpEffect(PowerUpType.Shield);
            SetUIState(shieldPanel, false);
        }

        if (!_keepSpeedOnRespawn)
        {
            if (IsSpeedActive()) PlayDestructionSound();
            _speedTime = 0;
            ResetPowerUpEffect(PowerUpType.Speed);
            SetUIState(speedPanel, false);
        }

        if (!_keepJumpOnRespawn)
        {
            if (IsJumpActive()) PlayDestructionSound();
            _jumpTime = 0;
            ResetPowerUpEffect(PowerUpType.Jump);
            SetUIState(jumpPanel, false);
        }
    }

    private string FormatTime(float timeInSeconds)
    {
        float t = Mathf.Max(0, timeInSeconds);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public bool IsShieldActive() => _shieldTime > 0;
    public bool IsSpeedActive() => _speedTime > 0;
    public bool IsJumpActive() => _jumpTime > 0;
    public float GetSpeedMultiplier() => _currentSpeedMultiplier;
    public float GetJumpMultiplier() => _currentJumpMultiplier;

    private void SetUIState(GameObject panel, bool state)
    {
        if (panel != null) panel.SetActive(state);
    }

    public void ResetPickUpManually(int typeIndex)
    {
        if (typeIndex == 0 && IsShieldActive())
        {
            _shieldTime = 0;
            PlayDestructionSound();
            ResetPowerUpEffect(PowerUpType.Shield);
            SetUIState(shieldPanel, false);
        }
        else if (typeIndex == 1 && IsSpeedActive())
        {
            _speedTime = 0;
            PlayDestructionSound();
            ResetPowerUpEffect(PowerUpType.Speed);
            SetUIState(speedPanel, false);
        }
        else if (typeIndex == 2 && IsJumpActive())
        {
            _jumpTime = 0;
            PlayDestructionSound();
            ResetPowerUpEffect(PowerUpType.Jump);
            SetUIState(jumpPanel, false);
        }
    }
}