using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsMenuController : MonoBehaviour
{
    private const string AttackActionPath = "Player/Attack";
    private const string PlayerMapName = "Player";
    private const string SlotActionPrefix = "Player/SelectSlot";

    [Serializable]
    public class RebindButton
    {
        public Button button;
        public TextMeshProUGUI label;
        public string actionPath;
        public string bindingPart;
        public string prefix;
        public bool keyboardOnly;
    }

    [Header("Volume")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Controls")]
    [SerializeField] private List<RebindButton> rebindButtons = new();

    [Header("Buttons")]
    [SerializeField] private Button resetBindingsButton;
    [SerializeField] private Button backButton;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;

    private InputActionAsset actions;
    private InputActionRebindingExtensions.RebindingOperation currentRebind;

    private void Awake()
    {
        actions = SettingsStorage.GetGlobalActions();
        SettingsStorage.ApplyInputOverrides(actions);
        InitializeUi();
    }

    private void OnDestroy()
    {
        currentRebind?.Dispose();
        currentRebind = null;
    }

    private void InitializeUi()
    {
        if (musicSlider != null)
        {
            musicSlider.SetValueWithoutNotify(SettingsStorage.MusicVolume);
            musicSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
            musicSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxSlider != null)
        {
            sfxSlider.SetValueWithoutNotify(SettingsStorage.SfxVolume);
            sfxSlider.onValueChanged.RemoveListener(OnSfxVolumeChanged);
            sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (resetBindingsButton != null)
        {
            resetBindingsButton.onClick.RemoveListener(ResetBindings);
            resetBindingsButton.onClick.AddListener(ResetBindings);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(BackToMenu);
            backButton.onClick.AddListener(BackToMenu);
        }

        for (int i = 0; i < rebindButtons.Count; i++)
        {
            RebindButton rebindButton = rebindButtons[i];
            if (rebindButton == null || rebindButton.button == null)
                continue;

            rebindButton.button.onClick.RemoveAllListeners();
            if (IsAttackRebind(rebindButton))
                continue;

            rebindButton.button.onClick.AddListener(() => StartRebind(rebindButton));
        }

        RefreshAllBindingLabels();
        SetStatus(string.Empty);
    }

    private void StartRebind(RebindButton target)
    {
        if (actions == null || target == null)
            return;

        if (IsAttackRebind(target))
            return;

        InputAction action = FindAction(target.actionPath);
        if (action == null)
            return;

        int bindingIndex = FindBindingIndex(action, target.bindingPart);
        if (bindingIndex < 0)
            return;

        string previousPath = GetBindingPath(action.bindings[bindingIndex]);

        currentRebind?.Dispose();
        currentRebind = null;

        SetStatus("\u041d\u0430\u0436\u043c\u0438\u0442\u0435 \u043d\u043e\u0432\u0443\u044e \u043a\u043b\u0430\u0432\u0438\u0448\u0443...");
        if (target.label != null)
            target.label.text = "\u0416\u0434\u0443...";

        action.Disable();

        currentRebind = action.PerformInteractiveRebinding(bindingIndex)
            .WithCancelingThrough("<Keyboard>/escape")
            .WithControlsHavingToMatchPath("<Keyboard>")
            .WithControlsExcluding("<Pointer>/position")
            .WithControlsExcluding("<Mouse>/delta")
            .WithControlsExcluding("<Mouse>")
            .OnCancel(operation =>
            {
                operation.Dispose();
                currentRebind = null;
                action.Enable();
                RefreshAllBindingLabels();
                SetStatus("\u041f\u0435\u0440\u0435\u043d\u0430\u0437\u043d\u0430\u0447\u0435\u043d\u0438\u0435 \u043e\u0442\u043c\u0435\u043d\u0435\u043d\u043e");
            })
            .OnComplete(operation =>
            {
                operation.Dispose();
                currentRebind = null;
                SwapConflictingBinding(target, action, bindingIndex, previousPath);
                action.Enable();
                SettingsStorage.SaveInputOverrides(actions);
                RefreshAllBindingLabels();
                SetStatus("\u0423\u043f\u0440\u0430\u0432\u043b\u0435\u043d\u0438\u0435 \u0441\u043e\u0445\u0440\u0430\u043d\u0435\u043d\u043e");
            });

        currentRebind.Start();
    }

    private void SwapConflictingBinding(RebindButton target, InputAction targetAction, int targetBindingIndex, string previousPath)
    {
        if (target == null || targetAction == null || targetBindingIndex < 0 || string.IsNullOrEmpty(previousPath))
            return;

        string newPath = GetBindingPath(targetAction.bindings[targetBindingIndex]);
        if (string.IsNullOrEmpty(newPath) ||
            string.Equals(newPath, previousPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        for (int i = 0; i < rebindButtons.Count; i++)
        {
            RebindButton other = rebindButtons[i];
            if (other == null || other == target || IsAttackRebind(other))
                continue;

            InputAction otherAction = FindAction(other.actionPath);
            if (otherAction == null)
                continue;

            int otherBindingIndex = FindBindingIndex(otherAction, other.bindingPart);
            if (otherBindingIndex < 0)
                continue;

            string otherPath = GetBindingPath(otherAction.bindings[otherBindingIndex]);
            if (!string.Equals(otherPath, newPath, StringComparison.OrdinalIgnoreCase))
                continue;

            bool wasEnabled = otherAction.enabled;
            if (wasEnabled)
                otherAction.Disable();

            otherAction.ApplyBindingOverride(otherBindingIndex, previousPath);

            if (wasEnabled)
                otherAction.Enable();

            return;
        }
    }

    private InputAction FindAction(string actionPath)
    {
        if (actions == null || string.IsNullOrWhiteSpace(actionPath))
            return null;

        InputAction action = actions.FindAction(actionPath, false);
        if (action != null)
            return action;

        string[] parts = actionPath.Split('/');
        string mapName = parts.Length == 2 ? parts[0] : PlayerMapName;
        string actionName = parts.Length == 2 ? parts[1] : actionPath;

        InputActionMap map = actions.FindActionMap(mapName, false);
        if (map != null)
        {
            action = map.FindAction(actionName, false);
            if (action != null)
                return action;
        }

        map = actions.FindActionMap(PlayerMapName, false);
        return map != null ? map.FindAction(actionName, false) : null;
    }

    private int FindBindingIndex(InputAction action, string bindingPart)
    {
        bool hasBindingPart = !string.IsNullOrWhiteSpace(bindingPart);

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];

            if (!hasBindingPart &&
                !binding.isComposite &&
                !binding.isPartOfComposite &&
                IsKeyboardBinding(binding))
            {
                return i;
            }

            if (hasBindingPart &&
                binding.isPartOfComposite &&
                string.Equals(binding.name, bindingPart, StringComparison.OrdinalIgnoreCase) &&
                IsKeyboardBinding(binding) &&
                !IsArrowBinding(binding))
            {
                return i;
            }
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (!hasBindingPart &&
                !binding.isComposite &&
                !binding.isPartOfComposite &&
                IsKeyboardBinding(binding))
            {
                return i;
            }

            if (hasBindingPart &&
                binding.isPartOfComposite &&
                string.Equals(binding.name, bindingPart, StringComparison.OrdinalIgnoreCase) &&
                IsKeyboardBinding(binding))
            {
                return i;
            }
        }

        return -1;
    }

    private string GetBindingPath(InputBinding binding)
    {
        if (!string.IsNullOrEmpty(binding.effectivePath))
            return binding.effectivePath;

        return binding.path;
    }

    private bool IsKeyboardBinding(InputBinding binding)
    {
        return !string.IsNullOrEmpty(binding.effectivePath) &&
               binding.effectivePath.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsArrowBinding(InputBinding binding)
    {
        return !string.IsNullOrEmpty(binding.effectivePath) &&
               binding.effectivePath.IndexOf("Arrow", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void RefreshAllBindingLabels()
    {
        for (int i = 0; i < rebindButtons.Count; i++)
        {
            if (IsAttackRebind(rebindButtons[i]))
                continue;

            RefreshBindingLabel(rebindButtons[i]);
        }
    }

    private void RefreshBindingLabel(RebindButton target)
    {
        if (actions == null || target == null || target.label == null)
            return;

        InputAction action = FindAction(target.actionPath);
        if (action == null)
            return;

        int bindingIndex = FindBindingIndex(action, target.bindingPart);
        if (bindingIndex < 0)
            return;

        target.label.text = target.prefix + GetBindingDisplayName(action.bindings[bindingIndex], IsSlotRebind(target));
    }

    private string GetBindingDisplayName(InputBinding binding, bool limitToTwoCharacters)
    {
        string path = binding.effectivePath;
        if (string.IsNullOrEmpty(path))
            path = binding.path;

        if (string.IsNullOrEmpty(path))
            return "--";

        string control = path;
        int slashIndex = control.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < control.Length - 1)
            control = control.Substring(slashIndex + 1);

        control = control.Replace("Arrow", string.Empty);
        control = control.Replace("Digit", string.Empty);
        control = control.Replace("Numpad", "Num");

        string upper = control.ToUpperInvariant();
        string result = upper switch
        {
            "SPACE" => "SP",
            "ENTER" => "Enter",
            "TAB" => "Tab",
            "ESCAPE" => "Esc",
            "LEFTSHIFT" => "LShift",
            "RIGHTSHIFT" => "RShift",
            "LEFTCTRL" => "LCtrl",
            "RIGHTCTRL" => "RCtrl",
            "LEFTALT" => "LAlt",
            "RIGHTALT" => "RAlt",
            "BACKSPACE" => "BS",
            _ => upper
        };

        if (limitToTwoCharacters && result.Length > 2)
            result = result.Substring(0, 2);

        return result;
    }

    private void OnMusicVolumeChanged(float value)
    {
        SettingsStorage.SaveMusicVolume(value);
        AudioController.Instance?.SetMusicVolume(value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        SettingsStorage.SaveSfxVolume(value);
        AudioController.Instance?.SetSfxVolume(value);
    }

    private void ResetBindings()
    {
        SettingsStorage.ClearInputOverrides(actions);
        RefreshAllBindingLabels();
        SetStatus("\u0423\u043f\u0440\u0430\u0432\u043b\u0435\u043d\u0438\u0435 \u0441\u0431\u0440\u043e\u0448\u0435\u043d\u043e");
    }

    private void BackToMenu()
    {
        SceneManager.LoadScene(SettingsStorage.MainMenuSceneName);
    }

    private void SetStatus(string text)
    {
        if (statusText != null)
            statusText.text = text;
    }

    private bool IsAttackRebind(RebindButton rebindButton)
    {
        return rebindButton != null &&
               string.Equals(rebindButton.actionPath, AttackActionPath, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSlotRebind(RebindButton rebindButton)
    {
        return rebindButton != null &&
               !string.IsNullOrEmpty(rebindButton.actionPath) &&
               rebindButton.actionPath.StartsWith(SlotActionPrefix, StringComparison.OrdinalIgnoreCase);
    }
}
