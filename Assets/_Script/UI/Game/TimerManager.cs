using UnityEngine;
using TMPro;

public class TimerManager : MonoBehaviour
{
    [Header("UI Text")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Timer Settings")]
    [SerializeField] [Range(10f, 5999f)] private float totalTimeInSeconds = 600f;
    [SerializeField] [Range(0f, 5999f)] private float countdownThresholdInSeconds = 60f;

    [Header("Effects Settings")]
    [SerializeField] [Range(0f, 5999f)] private float pumpThresholdInSeconds = 15f;
    [SerializeField] [Min(0f)] private float pumpSpeed = 5f;
    [SerializeField] [Min(0f)] private float pumpAmount = 0.15f;

    [Header("References")]
    [SerializeField] private GamePause gamePauseScript;
    [SerializeField] private EndGame endGameScript;

    private int _lastSecondBeep = -1;
    private float _currentTime;
    private bool _isGameOver = false;
    private Vector3 _originalScale;

    private void Start()
    {
        _currentTime = 0f;
        _originalScale = timerText.transform.localScale;
        _isGameOver = false;

        if (endGameScript == null)
            endGameScript = Object.FindFirstObjectByType<EndGame>();
    }

    private void Update()
    {
        if (_isGameOver || (endGameScript != null && endGameScript.isGameOver) || (gamePauseScript != null && gamePauseScript.paused))
        {
            return;
        }

        _currentTime += Time.deltaTime;
        float timeRemaining = totalTimeInSeconds - _currentTime;

        if (timeRemaining <= 0)
        {
            HandleGameOver();
        }
        else
        {
            UpdateUI(timeRemaining);
        }
    }

    private void UpdateUI(float timeRemaining)
    {
        if (timeRemaining <= countdownThresholdInSeconds)
        {
            timerText.color = Color.red;
            timerText.text = $"-{FormatTime(timeRemaining + 1)}";

            if (timeRemaining <= pumpThresholdInSeconds)
            {
                ApplyPumpingEffect();
                HandleCountdownAudio(timeRemaining);
            }
        }
        else
        {
            if (_lastSecondBeep != -1) StopPanicMode();

            timerText.color = Color.white;
            timerText.text = FormatTime(_currentTime);
            timerText.transform.localScale = _originalScale;
        }
    }

    private void HandleCountdownAudio(float timeRemaining)
    {
        if (AudioManager.Instance == null) return;

        if (_lastSecondBeep == -1)
        {
            AudioManager.Instance.SetMusicPanicMode(true);
        }

        int currentSecond = Mathf.CeilToInt(timeRemaining);

        if (currentSecond != _lastSecondBeep && currentSecond >= 0)
        {
            _lastSecondBeep = currentSecond;
            AudioManager.Instance.PlayCountdownTick();
        }
    }

    private void StopPanicMode()
    {
        _lastSecondBeep = -1;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SetMusicPanicMode(false);
    }

    private string FormatTime(float timeInSeconds)
    {
        float t = Mathf.Max(0, timeInSeconds);
        int minutes = Mathf.FloorToInt(t / 60f);
        int seconds = Mathf.FloorToInt(t % 60f);
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    public float GetRawTime()
    {
        return _currentTime;
    }

    private void ApplyPumpingEffect()
    {
        float scaleOffset = Mathf.Sin(Time.time * pumpSpeed) * pumpAmount;
        timerText.transform.localScale = _originalScale * (1f + scaleOffset);
    }

    private void HandleGameOver()
    {
        StopPanicMode();
        
        _isGameOver = true;
        timerText.text = "-00:00";
        timerText.transform.localScale = _originalScale;

        if (endGameScript != null)
        {
            endGameScript.Lose();
        }
        else
        {
            Debug.LogError("TimerManager: manca il riferimento a EndGame!");
        }
    }

    public void ResetTimer()
    {
        StopPanicMode();
        
        _currentTime = 0f;
        _isGameOver = false;
        timerText.transform.localScale = _originalScale;
    }
}