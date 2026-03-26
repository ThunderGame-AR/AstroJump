using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneChanger : MonoBehaviour
{
    public static SceneChanger Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] [Min(0.1f)] private float minLoadingTimeInSeconds = 1.0f;

    public bool IsBusy => _opsInFlight > 0;

    public event Action OnLoadingStarted;
    public event Action OnLoadingCompleted;
    public event Action<float> OnProgress;

    private int _opsInFlight;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void LoadSingleAsync(int buildIndex)
    {
        if (!IsBusy) StartCoroutine(LoadRoutine(buildIndex));
    }

    private IEnumerator LoadRoutine(int buildIndex)
    {
        _opsInFlight++;
        OnLoadingStarted?.Invoke();

        AudioListener.pause = true;
        if (AudioManager.Instance != null) AudioManager.Instance.StopAllAudio();

        yield return null;

        AsyncOperation op = SceneManager.LoadSceneAsync(buildIndex);
        op.allowSceneActivation = false;

        float timer = 0f;
        float visualProgress = 0f;

        while (op.progress < 0.9f || timer < minLoadingTimeInSeconds)
        {
            timer += Time.unscaledDeltaTime;

            float timerProgress = Mathf.Clamp01(timer / minLoadingTimeInSeconds);
            float realProgress = Mathf.Clamp01(op.progress / 0.9f);

            float targetProgress = Mathf.Max(timerProgress, realProgress) * 0.9f;

            float speed = 0.9f / minLoadingTimeInSeconds;
            visualProgress = Mathf.MoveTowards(visualProgress, targetProgress, speed * Time.unscaledDeltaTime);

            OnProgress?.Invoke(visualProgress);
            yield return null;
        }

        op.allowSceneActivation = true;

        while (!op.isDone)
        {
            visualProgress = Mathf.MoveTowards(visualProgress, 1f, Time.unscaledDeltaTime * 0.5f);

            OnProgress?.Invoke(visualProgress);
            yield return null;
        }

        OnProgress?.Invoke(1f);

        yield return new WaitForSecondsRealtime(0.15f);

        AudioListener.pause = false;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusicImmediate();

        OnLoadingCompleted?.Invoke();
        _opsInFlight--;
    }
}