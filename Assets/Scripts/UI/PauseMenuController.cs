using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PauseMenuController : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject pausePanel;

    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button saveAndExitButton;

    [Header("Optional")]
    [SerializeField] private PlayerController playerController;

    private bool isPaused;
    private float previousTimeScale = 1f;

    private void Awake()
    {
        ResolveReferences();
        SetPaused(false);
    }

    private void OnEnable()
    {
        BindButtons();
    }

    private void OnDisable()
    {
        UnbindButtons();

        if (isPaused)
            SetPaused(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            return;

        if (InventoryUI.HasAnyBlockingUiOpen && !isPaused)
            return;

        SetPaused(!isPaused);
    }

    public void Continue()
    {
        SetPaused(false);
    }

    public void Save()
    {
        SaveManager.SaveGame();
    }

    public void SaveAndExit()
    {
        SetPaused(false);
        SaveManager.SaveAndExitToMenu();
    }

    private void SetPaused(bool paused)
    {
        if (isPaused == paused && pausePanel != null && pausePanel.activeSelf == paused)
            return;

        if (paused)
            previousTimeScale = Time.timeScale;

        isPaused = paused;
        Time.timeScale = paused ? 0f : Mathf.Max(0.0001f, previousTimeScale);

        if (pausePanel != null)
            pausePanel.SetActive(paused);

        if (playerController != null)
            playerController.SetMovementLocked(paused);

        if (settingsButton != null)
            settingsButton.gameObject.SetActive(false);
    }

    private void BindButtons()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(Continue);
            continueButton.onClick.AddListener(Continue);
        }

        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(Save);
            saveButton.onClick.AddListener(Save);
        }

        if (saveAndExitButton != null)
        {
            saveAndExitButton.onClick.RemoveListener(SaveAndExit);
            saveAndExitButton.onClick.AddListener(SaveAndExit);
        }

        if (settingsButton != null)
            settingsButton.gameObject.SetActive(false);
    }

    private void UnbindButtons()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(Continue);

        if (saveButton != null)
            saveButton.onClick.RemoveListener(Save);

        if (saveAndExitButton != null)
            saveAndExitButton.onClick.RemoveListener(SaveAndExit);
    }

    private void ResolveReferences()
    {
        if (pausePanel == null)
            pausePanel = gameObject;

        if (playerController == null)
            playerController = FindFirstObjectByType<PlayerController>();
    }
}
