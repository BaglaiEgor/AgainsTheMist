using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class SettingsStorage
{
    public const string MainMenuSceneName = "MainMenu";
    public const string SettingsSceneName = "SettingScene";

    private const string MusicVolumeKey = "Settings.MusicVolume";
    private const string SfxVolumeKey = "Settings.SfxVolume";
    private const string InputOverridesKey = "Settings.InputOverrides";

    public static float MusicVolume => PlayerPrefs.GetFloat(MusicVolumeKey, 0.45f);
    public static float SfxVolume => PlayerPrefs.GetFloat(SfxVolumeKey, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        ApplyInputOverridesToGlobalActions();
        ApplyInputOverridesToScenePlayerInputs();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public static void SaveMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    public static void SaveSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    public static InputActionAsset GetGlobalActions()
    {
        return UnityEngine.InputSystem.InputSystem.actions;
    }

    public static void ApplyInputOverrides(InputActionAsset actions)
    {
        if (actions == null)
            return;

        actions.RemoveAllBindingOverrides();

        string json = PlayerPrefs.GetString(InputOverridesKey, string.Empty);
        if (!string.IsNullOrEmpty(json))
            actions.LoadBindingOverridesFromJson(json);
    }

    public static void SaveInputOverrides(InputActionAsset actions)
    {
        if (actions == null)
            return;

        PlayerPrefs.SetString(InputOverridesKey, actions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        ApplyInputOverridesToScenePlayerInputs();
    }

    public static void ClearInputOverrides(InputActionAsset actions)
    {
        if (actions != null)
            actions.RemoveAllBindingOverrides();

        PlayerPrefs.DeleteKey(InputOverridesKey);
        PlayerPrefs.Save();
        ApplyInputOverridesToScenePlayerInputs();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyInputOverridesToGlobalActions();
        ApplyInputOverridesToScenePlayerInputs();
    }

    private static void ApplyInputOverridesToGlobalActions()
    {
        ApplyInputOverrides(GetGlobalActions());
    }

    private static void ApplyInputOverridesToScenePlayerInputs()
    {
        PlayerInput[] playerInputs = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
        for (int i = 0; i < playerInputs.Length; i++)
        {
            PlayerInput playerInput = playerInputs[i];
            if (playerInput != null)
                ApplyInputOverrides(playerInput.actions);
        }
    }
}
