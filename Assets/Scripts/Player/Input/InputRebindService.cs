using UnityEngine;
using UnityEngine.InputSystem;

// 运行时改键：只动 Binding override，不改 Action 订阅。
// 必须作用在 PlayerInput 正在用的那份 InputActionAsset 上。
public static class InputRebindService
{
    public const string PrefsKey = "PlayerBindingOverrides";
    public const string KeyboardMouseGroup = "KeyboardMouse";
    public const string GamepadGroup = "Gamepad";

    public static readonly string[] RemappableActions =
    {
        "Attack", "Deflect", "Dodge", "Jump", "Heal", "LockOn"
    };

    public static readonly string[] RemappableLabels =
    {
        "攻击", "防御", "垫步", "跳跃", "葫芦", "锁定"
    };

    public static void Load(InputActionAsset asset)
    {
        if (asset == null) return;
        if (!PlayerPrefs.HasKey(PrefsKey)) return;

        string json = PlayerPrefs.GetString(PrefsKey);
        if (string.IsNullOrEmpty(json)) return;
        asset.LoadBindingOverridesFromJson(json);
    }

    public static void Save(InputActionAsset asset)
    {
        if (asset == null) return;
        PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static int FindBindingIndex(InputAction action, string group)
    {
        if (action == null) return -1;

        // 同一 Scheme 取第一条完整绑定（LockOn 手柄只保留 rightStickPress）
        for (int i = 0; i < action.bindings.Count; i++)
        {
            InputBinding binding = action.bindings[i];
            if (binding.isComposite || binding.isPartOfComposite) continue;
            if (string.IsNullOrEmpty(binding.groups) || !binding.groups.Contains(group)) continue;
            return i;
        }

        return -1;
    }

    public static string GetBindingDisplay(InputAction action, string group)
    {
        int index = FindBindingIndex(action, group);
        if (index < 0) return "—";
        return action.GetBindingDisplayString(index);
    }

    public static void ResetGroup(InputActionAsset asset, string group)
    {
        if (asset == null) return;

        for (int i = 0; i < RemappableActions.Length; i++)
        {
            InputAction action = asset.FindAction(RemappableActions[i]);
            int index = FindBindingIndex(action, group);
            if (index >= 0) action.RemoveBindingOverride(index);
        }

        Save(asset);
    }

    public static InputActionRebindingExtensions.RebindingOperation BeginRebind(
        InputAction action,
        int bindingIndex,
        string group,
        System.Action onApplied,
        System.Action onCancel)
    {
        string oldPath = action.bindings[bindingIndex].effectivePath;

        // PerformInteractiveRebinding 要求 Action 处于 Disable；暂停时 Player Map 仍是开的
        bool wasEnabled = action.enabled;
        action.Disable();

        InputActionRebindingExtensions.RebindingOperation operation;
        try
        {
            operation = action
                .PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough(group == KeyboardMouseGroup
                    ? "<Keyboard>/escape"
                    : "<Gamepad>/buttonEast")
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .WithControlsExcluding("<Mouse>/scroll")
                .WithControlsExcluding("<Keyboard>/escape")
                .WithControlsExcluding("<Gamepad>/startButton")
                .OnMatchWaitForAnother(0.1f);

            if (group == KeyboardMouseGroup)
            {
                operation.WithControlsExcluding("<Gamepad>");
                operation.WithControlsExcluding("<XInputController>");
            }
            else
            {
                operation.WithControlsExcluding("<Keyboard>");
                operation.WithControlsExcluding("<Mouse>");
            }

            operation
                .OnComplete(op =>
                {
                    SwapDuplicates(action.actionMap.asset, action, bindingIndex, group, oldPath);
                    Save(action.actionMap.asset);
                    op.Dispose();
                    if (wasEnabled) action.Enable();
                    onApplied?.Invoke();
                })
                .OnCancel(op =>
                {
                    op.Dispose();
                    if (wasEnabled) action.Enable();
                    onCancel?.Invoke();
                });

            return operation.Start();
        }
        catch
        {
            if (wasEnabled) action.Enable();
            throw;
        }
    }

    private static void SwapDuplicates(
        InputActionAsset asset,
        InputAction reboundAction,
        int reboundIndex,
        string group,
        string oldPath)
    {
        InputBinding rebound = reboundAction.bindings[reboundIndex];

        for (int i = 0; i < RemappableActions.Length; i++)
        {
            InputAction other = asset.FindAction(RemappableActions[i]);
            if (other == null || other == reboundAction) continue;

            int otherIndex = FindBindingIndex(other, group);
            if (otherIndex < 0) continue;
            if (!PathsConflict(rebound.effectivePath, other.bindings[otherIndex].effectivePath)) continue;

            other.ApplyBindingOverride(otherIndex, oldPath);
        }
    }

    private static bool PathsConflict(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        if (a == b) return true;
        return NormalizePath(a) == NormalizePath(b);
    }

    // <Keyboard>/j 与 /Keyboard/j 视为同一键
    private static string NormalizePath(string path)
    {
        return path.TrimStart('/').Replace("<", string.Empty).Replace(">", string.Empty);
    }
}
