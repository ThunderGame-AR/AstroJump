using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance;

    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Image loadingBar;

    [SerializeField] private TextMeshProUGUI loadingText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        loadingPanel.SetActive(false);
        StartCoroutine(BindEvents());
    }

    private IEnumerator BindEvents()
    {
        while (SceneChanger.Instance == null) yield return null;

        SceneChanger.Instance.OnLoadingStarted += Show;
        SceneChanger.Instance.OnLoadingCompleted += Hide;
        SceneChanger.Instance.OnProgress += UpdateUI;
    }

    private void OnDestroy()
    {
        if (SceneChanger.Instance != null)
        {
            SceneChanger.Instance.OnLoadingStarted -= Show;
            SceneChanger.Instance.OnLoadingCompleted -= Hide;
            SceneChanger.Instance.OnProgress -= UpdateUI;
        }
    }

    private void Show()
    {
        loadingBar.fillAmount = 0f;
        if (loadingText != null) loadingText.text = "Caricamento: 0%";
        loadingPanel.SetActive(true);
    }

    private void Hide() => loadingPanel.SetActive(false);

    private void UpdateUI(float progress)
    {
        loadingBar.fillAmount = progress;

        if (loadingText != null)
        {
            int percentage = Mathf.RoundToInt(progress * 100);
            loadingText.text = $"Caricamento: {percentage}%";
        }
    }
}