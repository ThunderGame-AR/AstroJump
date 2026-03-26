using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

public class StatisticsManager : MonoBehaviour
{
    [Header("UI Text")]
    [SerializeField] private TextMeshProUGUI statsText;

    [Header("References")]
    [SerializeField] private TimerManager timerManager;

    private void Update()
    {
        if (statsText != null && statsText.gameObject.activeInHierarchy)
        {
            statsText.text = GenerateStatsString();
        }
    }

    private string GenerateStatsString()
    {
        int currentLevel = SceneManager.GetActiveScene().buildIndex;

        float rawLevelTime = (timerManager != null) ? timerManager.GetRawTime() : 0f;

        float rawTotalTime = GlobalStats.TotalAccumulatedTimeInSeconds + rawLevelTime;

        string levelTimeFormatted = FormatLevelTime(Mathf.FloorToInt(rawLevelTime));
        string totalTimeFormatted = FormatTotalTime(Mathf.FloorToInt(rawTotalTime));

        return $"Livello: {currentLevel}\n" +
               $"Tempo nel livello: {levelTimeFormatted}\n" +
               $"Tempo totale: {totalTimeFormatted}";
    }

    private string FormatLevelTime(int totalSeconds)
    {
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private string FormatTotalTime(int totalSeconds)
    {
        int hours = totalSeconds / 3600;
        int minutes = (totalSeconds % 3600) / 60;
        int seconds = totalSeconds % 60;
        return string.Format("{0:00}:{1:00}:{2:00}", hours, minutes, seconds);
    }
}