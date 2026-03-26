using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;

public enum EffectAreaEndMode { None, Reset, Destroy }

[System.Flags]
public enum PickUpsEffectsToCancel { Shield = 1, Speed = 2, Jump = 4 }

[RequireComponent(typeof(BoxCollider))]
public class EffectArea : MonoBehaviour
{
    [Header("General Settings")]
    [Min(0f)] public float activateAfterSeconds = 0f;
    public bool activateAtRespawn = false;
    [ShowIf("activateAtRespawn")][Min(1)] public int respawnThresholdForActivation = 1;

    [Header("Area Modification")]
    public bool isLethalArea = false;
    [HideIf("isLethalArea")] public bool givesShieldInTheArea = false;
    public float areaMovementSpeed = 2.5f;
    public float areaJumpHeight = 1.5f;
    public float areaGravity = -9.81f;

    [Header("PickUps Effects Cancellation")]
    public PickUpsEffectsToCancel pickUpsEffectsToCancelInTheArea;

    [Header("Cycle Settings")]
    public EffectAreaEndMode endMode = EffectAreaEndMode.None;
    [HideIf("endMode", EffectAreaEndMode.None)][Min(2.5f)] public float activeDurationInSeconds = 10f;
    [HideIf("endMode", EffectAreaEndMode.None)] public bool startCountingAtRespawn = false;
    [ShowIf(EConditionOperator.And, "Condition_HasEndMode", "startCountingAtRespawn")][Min(1)] public int respawnThreshold = 1;

    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem areaParticles;
    [ColorUsage(true, true)]
    public Color areaAndParticlesColor = Color.white;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource areaAudioSource;
    [SerializeField] private AudioClip areaLoopMusic;
    [SerializeField] private AudioClip spawnSound;
    [SerializeField] private AudioClip despawnSound;

    private bool _isActive = false;
    private bool _isWaitingInitial = false;
    private float _timer = 0f;
    private PersonController _player;
    [HideInInspector] public bool _isPlayerInside = false;
    private BoxCollider _boxCollider;

    private float _origSpeed, _origJump, _origGravity;
    private bool _hasSavedOrigValues = false;

    public bool IsAreaActive() => _isActive;
    private bool Condition_HasEndMode => endMode != EffectAreaEndMode.None;

    void Awake()
    {
        _player = Object.FindFirstObjectByType<PersonController>();
        _boxCollider = GetComponent<BoxCollider>();

        if (_boxCollider != null) _boxCollider.isTrigger = true;
        if (areaParticles != null) areaParticles.Stop();

        if (areaAudioSource != null)
        {
            areaAudioSource.clip = areaLoopMusic;
            areaAudioSource.loop = true;
            areaAudioSource.spatialBlend = 1f;
            areaAudioSource.Stop();

            if (areaAudioSource.GetComponent<SpatialAudioPausable>() == null)
                areaAudioSource.gameObject.AddComponent<SpatialAudioPausable>();
        }

        StartInitialWait();
    }

    private void OnEnable() => PersonController.OnPlayerRespawn += HandleRespawn;
    private void OnDisable() => PersonController.OnPlayerRespawn -= HandleRespawn;

    private void OnValidate()
    {
        if (_boxCollider == null) _boxCollider = GetComponent<BoxCollider>();
        if (_boxCollider != null) _boxCollider.isTrigger = true;

        if (areaAudioSource != null)
        {
            areaAudioSource.loop = true;
            areaAudioSource.playOnAwake = false;
            areaAudioSource.spatialBlend = 1f;
            areaAudioSource.rolloffMode = AudioRolloffMode.Linear;

            areaAudioSource.dopplerLevel = 0f;
            areaAudioSource.spread = 180f;
            areaAudioSource.velocityUpdateMode = AudioVelocityUpdateMode.Fixed;
        }

        SyncParticlesWithCollider();
    }

    private void SyncParticlesWithCollider()
    {
        if (areaParticles == null || _boxCollider == null) return;

        var shape = areaParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = _boxCollider.size;
        shape.position = _boxCollider.center;
    }

    void StartInitialWait()
    {
        _isWaitingInitial = true;
        _timer = activateAfterSeconds;
        _isActive = false;
        _hasSavedOrigValues = false;
        if (areaParticles != null) areaParticles.Stop();
    }

    void Update()
    {
        if (Time.timeScale == 0) return;

        bool isGameOngoing = (_player != null && _player.enabled);

        if (_isWaitingInitial)
        {
            if (activateAtRespawn && _player != null && _player.numberTimesPlayerRespawned < respawnThresholdForActivation) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0) ActivateArea();
            return;
        }

        if (!_isActive || !isGameOngoing)
        {
            if (areaAudioSource.isPlaying)
            {
                areaAudioSource.volume = Mathf.MoveTowards(areaAudioSource.volume, 0f, Time.deltaTime * 5f);
                if (areaAudioSource.volume <= 0f) areaAudioSource.Stop();
            }
            return;
        }

        if (areaAudioSource != null && areaLoopMusic != null)
        {
            float targetSpatial = (_isPlayerInside) ? 0f : 1f;
            float targetVolume = (_isPlayerInside) ? 1f : 0f;

            areaAudioSource.spatialBlend = Mathf.MoveTowards(areaAudioSource.spatialBlend, targetSpatial, Time.deltaTime * 2f);
            areaAudioSource.volume = Mathf.MoveTowards(areaAudioSource.volume, targetVolume, Time.deltaTime * 2f);

            if (_isActive && !areaAudioSource.isPlaying) areaAudioSource.Play();
        }

        if (_isPlayerInside)
        {
            HandleContinuousPickupCancellation();

            if (isLethalArea)
            {
                bool hasPickupShield = PickUpManager.Instance != null && PickUpManager.Instance.IsShieldActive();
                if (!hasPickupShield) _player.Respawn();
            }
            else if (givesShieldInTheArea)
            {
                if (PickUpManager.Instance.shieldVisual != null && !PickUpManager.Instance.shieldVisual.activeSelf)
                {
                    PickUpManager.Instance.shieldVisual.SetActive(true);
                }
            }
        }

        if (endMode != EffectAreaEndMode.None)
        {
            bool canCount = !startCountingAtRespawn || (_player != null && _player.numberTimesPlayerRespawned >= respawnThreshold);
            if (canCount)
            {
                _timer -= Time.deltaTime;
                if (_timer <= 0) DeactivateArea();
            }
        }
    }

    private void HandleContinuousPickupCancellation()
    {
        if (pickUpsEffectsToCancelInTheArea == 0 || PickUpManager.Instance == null) return;

        if ((pickUpsEffectsToCancelInTheArea & PickUpsEffectsToCancel.Shield) != 0 && PickUpManager.Instance.IsShieldActive())
            PickUpManager.Instance.ResetPickUpManually(0);

        if ((pickUpsEffectsToCancelInTheArea & PickUpsEffectsToCancel.Speed) != 0 && PickUpManager.Instance.IsSpeedActive())
            PickUpManager.Instance.ResetPickUpManually(1);

        if ((pickUpsEffectsToCancelInTheArea & PickUpsEffectsToCancel.Jump) != 0 && PickUpManager.Instance.IsJumpActive())
            PickUpManager.Instance.ResetPickUpManually(2);
    }

    void ActivateArea()
    {
        _isWaitingInitial = false;
        _isActive = true;
        _timer = activeDurationInSeconds;

        if (areaParticles != null)
        {
            var main = areaParticles.main;
            main.startColor = areaAndParticlesColor;
            areaParticles.Play();
        }

        if (spawnSound != null)
            AudioSource.PlayClipAtPoint(spawnSound, transform.position, 1f);

        if (areaAudioSource != null && areaLoopMusic != null)
        {
            areaAudioSource.clip = areaLoopMusic;
            areaAudioSource.Play();
        }

        if (_isPlayerInside) ApplyEffect();
    }

    void DeactivateArea()
    {
        if (_isPlayerInside) ExitEffect();
        _isActive = false;
        if (areaParticles != null) areaParticles.Stop();

        if (despawnSound != null) AudioSource.PlayClipAtPoint(despawnSound, transform.position);
        if (areaAudioSource != null) StartCoroutine(FadeOutAndStop());

        if (endMode == EffectAreaEndMode.Destroy) Destroy(gameObject);
        else StartInitialWait();
    }

    private IEnumerator FadeOutAndStop()
    {
        float startVol = areaAudioSource.volume;
        while (areaAudioSource.volume > 0.01f)
        {
            areaAudioSource.volume -= startVol * Time.deltaTime * 5f;
            yield return null;
        }
        areaAudioSource.Stop();
    }

    public void ForceStopAudio()
    {
        if (areaAudioSource != null)
        {
            areaAudioSource.Stop();
            areaAudioSource.volume = 0f;
        }

        if (_isPlayerInside)
        {
            ExitEffect();
        }
    }

    void HandleRespawn()
    {
        if (_isPlayerInside)
        {
            ExitEffect();
            if (areaAudioSource != null) areaAudioSource.Stop();
        }

        if (endMode != EffectAreaEndMode.None && startCountingAtRespawn && _isActive)
        {
            if (_player.numberTimesPlayerRespawned >= respawnThreshold && _timer <= 0) DeactivateArea();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = true;
            if (_isActive) ApplyEffect();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInside = false;
            if (_isActive) ExitEffect();
        }
    }

    void ApplyEffect()
    {
        if (_player == null) return;

        if (pickUpsEffectsToCancelInTheArea != 0)
        {
            if ((pickUpsEffectsToCancelInTheArea & PickUpsEffectsToCancel.Shield) != 0)
                PickUpManager.Instance.ResetPickUpManually(0);

            if ((pickUpsEffectsToCancelInTheArea & PickUpsEffectsToCancel.Speed) != 0)
                PickUpManager.Instance.ResetPickUpManually(1);

            if ((pickUpsEffectsToCancelInTheArea & PickUpsEffectsToCancel.Jump) != 0)
                PickUpManager.Instance.ResetPickUpManually(2);
        }

        if (!_hasSavedOrigValues)
        {
            _origSpeed = _player.movementSpeed;
            _origJump = _player.jumpHeight;
            _origGravity = _player.gravity;
            _hasSavedOrigValues = true;
        }

        if (!isLethalArea && givesShieldInTheArea) PickUpManager.Instance.shieldVisual.SetActive(true);

        _player.movementSpeed = areaMovementSpeed;
        _player.jumpHeight = areaJumpHeight;
        _player.gravity = areaGravity;
    }

    public void ExitEffect()
    {
        if (_player == null || !_hasSavedOrigValues) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.ResetMusicVolume(1.2f);
        }

        if (!isLethalArea && givesShieldInTheArea && !PickUpManager.Instance.IsShieldActive())
            PickUpManager.Instance.shieldVisual.SetActive(false);

        _player.movementSpeed = _origSpeed;
        _player.jumpHeight = _origJump;
        _player.gravity = _origGravity;

        if ((_origGravity < 0 && _player.gravity > 0) || (_origGravity > 0 && _player.gravity < 0))
            _player.verticalVelocity = 0;

        _hasSavedOrigValues = false;
    }

    private void OnDrawGizmos()
    {
        BoxCollider coll = GetComponent<BoxCollider>();
        if (coll == null) return;

        Gizmos.matrix = transform.localToWorldMatrix;
        Vector3 center = coll.center;
        Vector3 size = coll.size;

        Color c = areaAndParticlesColor;
        c.a = 0.15f;
        Gizmos.color = c;
        Gizmos.DrawCube(center, size);

        Gizmos.color = areaAndParticlesColor;
        Gizmos.DrawWireCube(center, size);

        #if UNITY_EDITOR
        Handles.color = areaAndParticlesColor;
        Handles.matrix = transform.localToWorldMatrix;
        Handles.DrawWireCube(center, new Vector3(size.x, size.y, 0));
        Handles.DrawWireCube(center, new Vector3(size.x, 0, size.z));
        Handles.DrawWireCube(center, new Vector3(0, size.y, size.z));
        #endif
    }
}