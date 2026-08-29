using UnityEngine;
using UnityEngine.InputSystem;

// 战斗时锁定并隐藏鼠标；暂停 / 回生 / 结算 UI 时释放，避免指针飞出窗口或切回战斗时镜头狂转。
// 纯手柄操作时不显示系统光标（靠 UI 导航）；切回键鼠后 UI 内恢复可见。
public static class CursorController
{
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

    // 非暂停类 UI（回生选项、Game Over 等）占用光标时 +1；关闭时 PopUi。
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
            // 手柄走 UI 导航，不需要系统光标；键鼠才显示以便点按
            Cursor.visible = !gamepad;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (wasUiMode)
                orbitInput?.IgnoreLookUntil(Time.unscaledTime + 0.2f);
        }

        wasUiMode = ui;
    }
}
