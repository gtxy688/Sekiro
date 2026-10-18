using UnityEngine;
using UnityEngine.InputSystem;

using ARPG.Camera;
using ARPG.Player;
namespace ARPG.Mgr
{

    // 根据战斗、菜单和控制方案统一设置光标的锁定与显隐。
    public static class CursorController
    {
        // 多个界面可同时占用光标，全部关闭后才恢复战斗模式。
        private static int uiHoldCount;
        private static bool wasUiMode;
        private static CinemachineOrbitInput orbitInput;
        private static PlayerInput playerInput;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            uiHoldCount = 0;
            wasUiMode = false;
            orbitInput = null;
            UnbindPlayerInput();
        }

        public static void RegisterOrbitInput(CinemachineOrbitInput input)
        {
            orbitInput = input;
        }

        // 跟随控制方案变化刷新光标，替换输入组件时先解除旧订阅。
        public static void BindPlayerInput(PlayerInput input)
        {
            if (playerInput == input) return;
            UnbindPlayerInput();
            playerInput = input;
            if (playerInput != null)
                playerInput.onControlsChanged += HandleControlsChanged;
            Apply();
        }

        private static void UnbindPlayerInput()
        {
            if (playerInput != null)
                playerInput.onControlsChanged -= HandleControlsChanged;
            playerInput = null;
        }

        private static void HandleControlsChanged(PlayerInput _)
        {
            Apply();
        }

        // 回生、结算等界面打开时占用光标，关闭时必须配对调用 PopUi。
        public static void PushUi()
        {
            uiHoldCount++;
            Apply();
        }

        public static void PopUi()
        {
            if (uiHoldCount > 0)
                uiHoldCount--;
            Apply();
        }

        public static void Refresh()
        {
            Apply();
        }

        private static bool IsGamepadPrimary()
        {
            return InputRebindService.ResolveControlGroup(playerInput) == InputRebindService.GamepadGroup;
        }

        private static void Apply()
        {
            bool ui = uiHoldCount > 0 || GamePause.IsPaused || CombatInputGate.Blocked;
            bool gamepad = IsGamepadPrimary();

            if (ui)
            {
                Cursor.lockState = CursorLockMode.None;
                // 手柄通过 UI 导航操作，只有键鼠需要显示系统光标。
                Cursor.visible = !gamepad;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                // 从界面回到战斗时短暂忽略视角输入，避免光标归位带动镜头。
                if (wasUiMode)
                    orbitInput?.IgnoreLookUntil(Time.unscaledTime + 0.2f);
            }

            wasUiMode = ui;
        }
    }

}
