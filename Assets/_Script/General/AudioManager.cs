using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [Header("Music Settings")]
    public AudioSource musicSource;
    [Range(0f, 1f)] public float defaultMusicVolume = 0.2f;
    [HideInInspector] public bool isPaused = false;

    [Header("Countdown Settings")]
    [SerializeField] private AudioClip countdownTickSound;

    private void Start()
    {
        isPaused = false;

        if (musicSource != null)
        {
            musicSource.Stop();
            musicSource.playOnAwake = false;
            musicSource.enabled = false;
        }

        StopAllCoroutines();
        StartCoroutine(SafeMusicStartRoutine());
    }

    private IEnumerator SafeMusicStartRoutine()
    {
        if (SceneChanger.Instance != null)
        {
            while (SceneChanger.Instance.IsBusy)
            {
                yield return null;
            }
        }

        yield return new WaitForSeconds(0.5f);

        bool isGamePaused = (GamePause.Instance != null && GamePause.Instance.paused);

        if (!isGamePaused && !isPaused && musicSource != null)
        {
            musicSource.enabled = true;
            musicSource.Play();
        }
    }

    public void SetPauseState(bool state)
    {
        isPaused = state;
        if (musicSource != null)
        {
            if (state)
            {
                musicSource.Pause();
            }
            else
            {
                musicSource.enabled = true;
                musicSource.UnPause();
            }
        }
    }

    public void StopMusicImmediate()
    {
        if (musicSource != null) musicSource.Stop();
    }

    public void PlayMusicImmediate()
    {
        if (musicSource != null && !musicSource.isPlaying) musicSource.Play();
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        if (musicSource == null) musicSource = GetComponent<AudioSource>();

        musicSource.enabled = true;
        musicSource.spatialBlend = 0f;
        musicSource.loop = true;
        musicSource.volume = defaultMusicVolume;
    }

    public void PlayMusic(AudioClip clip)
    {
        musicSource.clip = clip;
        musicSource.Play();
    }

    public void FadeMusicVolume(float targetVolume, float duration)
    {
        StartCoroutine(LerpMusicVolume(targetVolume, duration));
    }

    public void ResetMusicVolume(float duration)
    {
        StartCoroutine(LerpMusicVolume(defaultMusicVolume, duration));
    }

    private IEnumerator LerpMusicVolume(float target, float duration)
    {
        float startVol = musicSource.volume;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            musicSource.volume = Mathf.Lerp(startVol, target, elapsed / duration);
            yield return null;
        }
        musicSource.volume = target;
    }

    public void StopAllAreaMusics()
    {
        EffectArea[] allAreas = Object.FindObjectsByType<EffectArea>(FindObjectsSortMode.None);

        foreach (EffectArea area in allAreas)
        {
            area.ForceStopAudio();
        }

        ResetMusicVolume(defaultMusicVolume);
    }

    public void StopAllAudio()
    {
        if (musicSource != null) musicSource.Stop();

        AudioSource[] allSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var source in allSources)
        {
            source.Stop();
        }
    }

    public void PlayCountdownTick()
    {
        if (countdownTickSound != null)
            musicSource.PlayOneShot(countdownTickSound, 1f);
    }

    public void SetMusicPanicMode(bool active)
    {
        float targetVolume = active ? 0.3f : 1f;
        FadeMusicVolume(targetVolume, 0.2f);
    }
}