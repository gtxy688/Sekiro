using UnityEngine;
using UnityEngine.InputSystem;

namespace ARPG.Player
{

    // 在 PlayerInput 的动作资产上读写改键覆盖，不改变动作事件订阅。
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

        // 从本地存档恢复绑定覆盖，未保存过改键时沿用资产默认绑定。
        public static void Load(InputActionAsset asset)
        {
            if (!PlayerPrefs.HasKey(PrefsKey)) return;

            string json = PlayerPrefs.GetString(PrefsKey);
            if (string.IsNullOrEmpty(json)) return;
            asset.LoadBindingOverridesFromJson(json);
        }

        // 优先使用 PlayerInput 的控制方案，尚未选定时参考最近更新的设备。
        public static string ResolveControlGroup(PlayerInput input)
        {
            if (input != null)
            {
                string scheme = input.currentControlScheme;
                if (scheme == GamepadGroup) return GamepadGroup;
                if (scheme == KeyboardMouseGroup) return KeyboardMouseGroup;
            }

            double bestTime = double.NegativeInfinity;
            bool gamepad = false;
            foreach (InputDevice device in InputSystem.devices)
            {
                if (!device.added) continue;
                if (device.lastUpdateTime < bestTime) continue;
                bestTime = device.lastUpdateTime;
                gamepad = device is Gamepad;
            }

            return gamepad ? GamepadGroup : KeyboardMouseGroup;
        }

        public static void Save(InputActionAsset asset)
        {
            PlayerPrefs.SetString(PrefsKey, asset.SaveBindingOverridesAsJson());
            PlayerPrefs.Save();
        }

        // 返回指定设备组的首条非组合绑定，找不到时返回 -1。
        public static int FindBindingIndex(InputAction action, string group)
        {
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
            return index >= 0 ? action.GetBindingDisplayString(index) : "—";
        }

        // 只恢复指定设备组的可改键动作，保留另一组的设置。
        public static void ResetGroup(InputActionAsset asset, string group)
        {
            for (int i = 0; i < RemappableActions.Length; i++)
            {
                InputAction action = asset.FindAction(RemappableActions[i]);
                int index = FindBindingIndex(action, group);
                if (index >= 0) action.RemoveBindingOverride(index);
            }

            Save(asset);
        }

        // 启动交互改键；完成或取消后恢复动作启用状态，调用方负责取消仍在等待的操作。
        public static InputActionRebindingExtensions.RebindingOperation BeginRebind(
            InputAction action,
            int bindingIndex,
            string group,
            System.Action onApplied,
            System.Action onCancel)
        {
            string oldPath = action.bindings[bindingIndex].effectivePath;

            // 交互改键要求动作停用；暂停菜单本身不会关闭 Player Map。
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

        // 新按键被其他动作占用时，将那个动作换到本次修改前的按键。
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
                if (other == reboundAction) continue;

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

        // 统一路径格式，让 <Keyboard>/j 与 /Keyboard/j 按同一按键比较。
        private static string NormalizePath(string path)
        {
            return path.TrimStart('/').Replace("<", string.Empty).Replace(">", string.Empty);
        }
    }

}
