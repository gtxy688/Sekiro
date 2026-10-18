using UnityEngine;
using UnityEngine.InputSystem;

using ARPG.Mgr;
namespace ARPG.Combat
{
    // 用热键触发本场复位，便于验证不重载场景的恢复流程。
    // 直接读 Keyboard，保证 PlayerInput 或战斗输入被关闭后仍能触发。
    public class EncounterResetDebug : MonoBehaviour
    {
        [Header("验证用热键")]
        [Tooltip("触发一次 EncounterScope.ResetAll()，等价于复战重开")]
        [SerializeField] private Key resetKey = Key.F8;

        private void Update()
        {
            if (GamePause.IsPaused) return;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[resetKey].wasPressedThisFrame) return;

            if (EncounterScope.Current == null)
            {
                Debug.LogError(
                    "[EncounterResetDebug] 场景里没有 EncounterScope，ResetAll() 无从调用。\n" +
                    "在场景任意物体上挂 EncounterScope，把玩家拖进 Player、Boss 拖进 Opponents。", this);
                return;
            }

            EncounterScope.Current.ResetAll();
            Debug.Log("[EncounterResetDebug] ResetAll() 已完成。", this);
        }
    }
}
