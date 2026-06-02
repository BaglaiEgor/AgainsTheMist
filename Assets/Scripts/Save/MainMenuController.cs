using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MainMenuController : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;

    private void OnEnable()
    {
        BindButtons();
        RefreshState();
    }

    private void OnDisable()
    {
        UnbindButtons();
    }

    public void RefreshState()
    {
        if (continueButton != null)
            continueButton.interactable = SaveManager.HasSave();

        if (settingsButton != null)
            settingsButton.interactable = true;
    }

    public void Configure(Button continueBtn, Button newGameBtn, Button settingsBtn, Button exitBtn)
    {
        UnbindButtons();

        continueButton = continueBtn;
        newGameButton = newGameBtn;
        settingsButton = settingsBtn;
        exitButton = exitBtn;

        BindButtons();
        RefreshState();
    }

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnContinueClicked);
            continueButton.onClick.AddListener(OnContinueClicked);
        }

        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
            newGameButton.onClick.AddListener(OnNewGameClicked);
        }

        if (exitButton != null)
        {
            exitButton.onClick.RemoveListener(OnExitClicked);
            exitButton.onClick.AddListener(OnExitClicked);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
    }

    private void UnbindButtons()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);

        if (newGameButton != null)
            newGameButton.onClick.RemoveListener(OnNewGameClicked);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(OnExitClicked);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OnSettingsClicked);
    }

    private void OnContinueClicked()
    {
        SaveManager.ContinueGame();
    }

    private void OnNewGameClicked()
    {
        SaveManager.StartNewGame();
    }

    private void OnSettingsClicked()
    {
        SceneManager.LoadScene(SettingsStorage.SettingsSceneName);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
