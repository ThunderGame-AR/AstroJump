using UnityEngine;

public class CameraModeManager : MonoBehaviour
{
    public static CameraModeManager Instance { get; private set; }

    public enum ViewMode { FirstPerson, ThirdPerson }
    public ViewMode currentViewMode = ViewMode.ThirdPerson;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void SetViewMode(ViewMode mode)
    {
        currentViewMode = mode;
    }
}