using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GamePause : MonoBehaviour
{
    private PlayerInputActions _playerInput;

    [Header("UI Containers")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject commonObject;

    [Header("Buttons")]
    [SerializeField] private Button unpauseButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button mainMenuButton;

    [Header("Settings")]
    [SerializeField] private bool startPaused = true;
    [SerializeField] [Min(0)] private int menuSceneIndex = 0;

    [Header("Reference")]
    [SerializeField] private EndGame endGameScript;

    [HideInInspector] public bool paused;
    public static GamePause Instance;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        _playerInput = new PlayerInputActions();
        _playerInput.Player.Pause.performed += OnPausePerformed;

        if (unpauseButton != null) unpauseButton.onClick.AddListener(TogglePause);
        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(ReturnToMenu);
    }

    private void OnEnable() => _playerInput?.Enable();
    private void OnDisable() => _playerInput?.Disable();

    private void Start()
    {
        SetPaused(startPaused);
    }

    private void OnDestroy()
    {
        if (_playerInput != null) _playerInput.Player.Pause.performed -= OnPausePerformed;
        _playerInput?.Dispose();
    }

    private void OnPausePerformed(InputAction.CallbackContext ctx)
    {
        if (endGameScript != null && endGameScript.isGameOver) return;
        TogglePause();
    }

    public void TogglePause() => SetPaused(!paused);

    public void SetPaused(bool value)
    {
        paused = value;

        if (pausePanel != null) pausePanel.SetActive(paused);
        if (commonObject != null) commonObject.SetActive(paused);

        AudioListener.pause = paused;

        Time.timeScale = paused ? 0f : 1f;

        if (AudioManager.Instance != null) AudioManager.Instance.SetPauseState(paused);

        Utilities.SetCursorLocked(!paused);

        if (paused)
        {
            if (VRMenuFocusManager.Instance != null)
            {
                VRMenuFocusManager.Instance.FocusButton(unpauseButton);
            }
        }
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetPauseState(false);
            AudioManager.Instance.StopAllAudio();
        }

        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneChanger.Instance.LoadSingleAsync(currentSceneIndex);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetPauseState(false);
            AudioManager.Instance.StopAllAudio();
        }

        GlobalStats.ResetTotalTime();
        SceneChanger.Instance.LoadSingleAsync(menuSceneIndex);
    }
}