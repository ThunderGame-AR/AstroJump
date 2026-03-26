using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private Button startFirstPersonButton;
    [SerializeField] private Button startThirdPersonButton;
    [SerializeField][Min(0)] private int gameSceneIndex = 1;

    PlayerInputActions inputActions;

    private void Awake()
    {
        inputActions = new PlayerInputActions();

        if (startFirstPersonButton != null)
            startFirstPersonButton.onClick.AddListener(() => StartGame(CameraModeManager.ViewMode.FirstPerson));

        if (startThirdPersonButton != null)
            startThirdPersonButton.onClick.AddListener(() => StartGame(CameraModeManager.ViewMode.ThirdPerson));
    }

    void OnEnable() => inputActions.Enable();

    void OnDisable() => inputActions.Disable();

    private void StartGame(CameraModeManager.ViewMode mode)
    {
        if (SceneChanger.Instance != null && !SceneChanger.Instance.IsBusy)
        {
            if (CameraModeManager.Instance != null)
            {
                CameraModeManager.Instance.SetViewMode(mode);
            }

            if (AudioManager.Instance != null) AudioManager.Instance.StopAllAudio();

            if (startFirstPersonButton != null) startFirstPersonButton.interactable = false;
            if (startThirdPersonButton != null) startThirdPersonButton.interactable = false;

            SceneChanger.Instance.LoadSingleAsync(gameSceneIndex);
        }
    }
}