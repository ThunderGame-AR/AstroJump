using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
public class TeleportManager : MonoBehaviour
{
    public enum PortalType { Vertical, HorizontalTop, HorizontalBottom }

    [Header("Connection & Identity")]
    [Min(1)] public int portalKey = 1;
    public PortalType teleportType;
    [Min(0f)] public float delayInSecondsToTeleport = 2.5f;

    [Header("Visual Effects")]
    [SerializeField] private GameObject vortexObject;
    [SerializeField] private ParticleSystem portalEffect;
    [ColorUsage(true, true)]
    public Color particlesColor = Color.violet;
    [SerializeField][Min(0f)] private float maxEmissionRate = 80f;
    [SerializeField][Min(0f)] private float maxParticleSpeed = 4f;

    [Header("Audio Settings")]
    [SerializeField] private AudioSource portalAudioSource;
    [SerializeField] private AudioClip teleportSound;
    [Min(0f)] public float audioStartDelayInSeconds = 0.75f;

    private float _myGlitchInterval;
    private float _minGlitchIntervalInSeconds = 0.2f;
    private float _maxGlitchIntervalInSeconds = 1f;

    private bool _isPlayerInside = false;
    private bool _canTeleport = true;
    private bool _isGlitching = false;
    private bool _justArrived = false;

    private string playerTag = "Player";

    private Rigidbody _rb;
    private Coroutine _activeRoutine;
    private Coroutine _audioRoutine;
    private ParticleSystem.EmissionModule _emission;
    private ParticleSystem.MainModule _main;

    private void OnEnable() => PersonController.OnPlayerRespawn += HandleGlobalRespawn;
    private void OnDisable() => PersonController.OnPlayerRespawn -= HandleGlobalRespawn;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = true;
        
        if (portalEffect != null)
        {
            _emission = portalEffect.emission;
            _main = portalEffect.main;
            _main.startColor = particlesColor;
            _emission.rateOverTime = 0f;
        }

        Random.InitState(portalKey);
        _myGlitchInterval = Random.Range(_minGlitchIntervalInSeconds, _maxGlitchIntervalInSeconds);
    }

    private void Start()
    {
        CheckIntegrity(false);
    }

    private void Update()
    {
        if (_isGlitching && vortexObject != null)
        {
            bool state = (Time.time % (_myGlitchInterval * 2)) < _myGlitchInterval;
            vortexObject.SetActive(state);
        }
    }

    private void HandleGlobalRespawn()
    {
        _isPlayerInside = false;
        _justArrived = false;
        StopAllCoroutines();
        StopAudioRoutine();
        if (portalEffect != null) _emission.rateOverTime = 0f;
    }

    private void StopAudioRoutine()
    {
        if (_audioRoutine != null) StopCoroutine(_audioRoutine);
        _audioRoutine = null;

        if (portalAudioSource != null)
        {
            portalAudioSource.Stop();
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.ResetMusicVolume(1f);
    }

    private IEnumerator PlayTeleportAudio()
    {
        if (portalAudioSource == null || teleportSound == null) yield break;

        yield return new WaitForSeconds(audioStartDelayInSeconds);

        if (!_isPlayerInside) yield break;

        portalAudioSource.clip = teleportSound;
        portalAudioSource.Play();

        if (AudioManager.Instance != null)
            AudioManager.Instance.FadeMusicVolume(0.3f, 1f);
    }

    private void CheckIntegrity(bool showErrors)
    {
        TeleportManager[] allPortals = Object.FindObjectsByType<TeleportManager>(FindObjectsSortMode.None);
        int matches = 0;

        foreach (var p in allPortals)
        {
            if (p.portalKey == this.portalKey && p != this) matches++;
        }

        if (matches != 1)
        {
            _canTeleport = false;
            _isGlitching = true;
            if (showErrors)
                Debug.LogError($"IMPOSSIBILE TELETRASPORTARSI: Il portale con chiave {portalKey} è solo o ha più di un partner!");
        }
        else
        {
            _canTeleport = true;
            _isGlitching = false;
            if (vortexObject != null) vortexObject.SetActive(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            if (_justArrived) return;

            CheckIntegrity(true);

            if (_canTeleport)
            {
                _isPlayerInside = true;
                TeleportManager dest = GetPartnerSilently();

                if (dest != null)
                {
                    StopActiveRoutine();
                    _activeRoutine = StartCoroutine(TeleportSequence(other.gameObject, dest));

                    StopAudioRoutine();
                    _audioRoutine = StartCoroutine(PlayTeleportAudio());

                    dest.StopActiveRoutine();
                    dest.SyncArrivalVisuals(delayInSecondsToTeleport);
                }
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            _isPlayerInside = false;
            _justArrived = false;
            StopTeleportAndSync();
            StopAudioRoutine();
        }
    }

    private void StopTeleportAndSync()
    {
        StopActiveRoutine();
        _activeRoutine = StartCoroutine(FadeParticles(0.5f, 0f));

        TeleportManager dest = GetPartnerSilently();
        if (dest != null)
        {
            dest.StopActiveRoutine();
            dest._activeRoutine = dest.StartCoroutine(dest.FadeParticles(0.5f, 0f));
        }
    }

    public void SyncArrivalVisuals(float duration)
    {
        StopActiveRoutine();
        _activeRoutine = StartCoroutine(AnimateParticles(duration));
    }

    private IEnumerator TeleportSequence(GameObject player, TeleportManager destination)
    {
        float elapsed = 0f;

        while (elapsed < delayInSecondsToTeleport)
        {
            if (!_isPlayerInside) yield break;

            elapsed += Time.deltaTime;
            float t = elapsed / delayInSecondsToTeleport;
            _emission.rateOverTime = t * maxEmissionRate;
            _main.startSpeed = t * maxParticleSpeed;
            yield return null;
        }

        if (_isPlayerInside)
        {
            destination._justArrived = true;
            ApplyTeleport(player, destination);
        }

        StopTeleportAndSync();
    }

    private IEnumerator AnimateParticles(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _emission.rateOverTime = t * maxEmissionRate;
            _main.startSpeed = t * maxParticleSpeed;
            yield return null;
        }
    }

    public IEnumerator FadeParticles(float duration, float targetRate)
    {
        if (portalEffect == null) yield break;
        float startRate = _emission.rateOverTime.constant;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _emission.rateOverTime = Mathf.Lerp(startRate, targetRate, elapsed / duration);
            yield return null;
        }
        _emission.rateOverTime = targetRate;
    }

    private void StopActiveRoutine()
    {
        if (_activeRoutine != null) StopCoroutine(_activeRoutine);
        _activeRoutine = null;
    }

    private TeleportManager GetPartnerSilently()
    {
        TeleportManager[] allPortals = Object.FindObjectsByType<TeleportManager>(FindObjectsSortMode.None);
        foreach (var p in allPortals)
        {
            if (p.portalKey == this.portalKey && p != this) return p;
        }
        return null;
    }

    private void ApplyTeleport(GameObject player, TeleportManager dest)
    {
        PersonController pc = player.GetComponent<PersonController>();

        if (pc != null && pc.currentActiveArea != null)
        {
            pc.currentActiveArea.ExitEffect();
            pc.currentActiveArea._isPlayerInside = false;
            pc.currentActiveArea = null;
        }

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        float yOffset = 0f;
        switch (dest.teleportType)
        {
            case PortalType.Vertical: yOffset = -3.92f; break;
            case PortalType.HorizontalTop: yOffset = -1.1f; break;
            case PortalType.HorizontalBottom: yOffset = -4.9f; break;
        }

        player.transform.position = dest.transform.position + new Vector3(0, yOffset, 0);
        player.transform.rotation = Quaternion.Euler(0, dest.transform.rotation.eulerAngles.y, 0);

        if (cc != null) cc.enabled = true;
    }
}