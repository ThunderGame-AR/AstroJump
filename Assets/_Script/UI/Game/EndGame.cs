using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EndGame : MonoBehaviour
{
    [Header("Panels & Containers")]
    [SerializeField] private GameObject winScreen;
    [SerializeField] private GameObject loseScreen;
    [SerializeField] private GameObject commonObject;

    [Header("Buttons")]
    [SerializeField] private Button nextLevelButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Scene Settings")]
    [SerializeField] [Min(0)] private int menuSceneIndex = 0;

    [Header("References")]
    [SerializeField] private TimerManager timerManager;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip winSound;
    [SerializeField] private AudioClip loseSound;

    [HideInInspector] public bool isGameOver = false;

    private void Awake()
    {
        if (nextLevelButton != null)
            nextLevelButton.onClick.AddListener(LoadNextLevel);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartLevel);

        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(ReturnToMenu);

        ResetUI();
    }

    private void ResetUI()
    {
        isGameOver = false;
        if (winScreen != null) winScreen.SetActive(false);
        if (loseScreen != null) loseScreen.SetActive(false);
    }

    public void Win()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAllAreaMusics();
            AudioManager.Instance.StopAllAudio();
        }
        HandleEndGameAudio(winSound);

        ShowEndScreen(true);
    }

    public void Lose()
    {
        if (isGameOver) return;
        isGameOver = true;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopAllAreaMusics();
            AudioManager.Instance.StopAllAudio();
        }
        HandleEndGameAudio(loseSound);

        ShowEndScreen(false);
    }

    private void HandleEndGameAudio(AudioClip endClip)
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetPauseState(true);

            if (endClip != null)
            {
                AudioSource.PlayClipAtPoint(endClip, Camera.main.transform.position, 1f);
            }
        }
    }

    private void ShowEndScreen(bool won)
    {
        Time.timeScale = 0f;
        
        Utilities.SetCursorLocked(false);

        if (winScreen != null) winScreen.SetActive(won);
        if (loseScreen != null) loseScreen.SetActive(!won);
        if (commonObject != null) commonObject.SetActive(true);

        if (won && nextLevelButton != null)
        {
            int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
            bool hasNextLevel = nextSceneIndex < SceneManager.sceneCountInBuildSettings;
            nextLevelButton.gameObject.SetActive(hasNextLevel);
        }

        if (won)
        {
            if (nextLevelButton != null && nextLevelButton.gameObject.activeSelf)
                VRMenuFocusManager.Instance.FocusButton(nextLevelButton);
            else
                VRMenuFocusManager.Instance.FocusButton(restartButton);
        }
        else
        {
            VRMenuFocusManager.Instance.FocusButton(restartButton);
        }
    }

    public void RestartLevel()
    {
        PrepareForSceneChange();

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;

        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadSingleAsync(currentSceneIndex);
        else
            SceneManager.LoadScene(currentSceneIndex);
    }

    public void LoadNextLevel()
    {
        PrepareForSceneChange();

        if (timerManager != null)
        {
            GlobalStats.AddLevelTime(Mathf.FloorToInt(timerManager.GetRawTime()));
        }

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadSingleAsync(nextSceneIndex);
        else
            SceneManager.LoadScene(nextSceneIndex);
    }

    public void ReturnToMenu()
    {
        PrepareForSceneChange();

        GlobalStats.ResetTotalTime();

        if (SceneChanger.Instance != null)
            SceneChanger.Instance.LoadSingleAsync(menuSceneIndex);
        else
            SceneManager.LoadScene(menuSceneIndex);
    }

    private void PrepareForSceneChange()
    {
        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.isPaused = false;
            if (AudioManager.Instance.musicSource != null)
                AudioManager.Instance.musicSource.enabled = true;

            AudioManager.Instance.StopAllAudio();
        }
    }
}