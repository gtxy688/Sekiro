using UnityEngine;
using UnityEngine.InputSystem;

using ARPG.Mgr;
namespace ARPG.Combat
{
    // 【临时】复战复位契约的验证触发器。验收走完即可删除本文件。
    //
    // 存在理由：目前项目里没有「复战 / 连战」流程，ResetAll() 没有任何调用点，
    // 不挂个触发器就根本没法验收——写完了但没人能按下那个按钮，等于没写。
    //
    // 为什么读 Keyboard.current 而不是走 PlayerInput：
    // 胜利时 PlayerInput 会被 DeactivateInput() 关掉、CombatInputGate 也会 Block，
    // 而「复位后输入能不能恢复」恰恰是这次要验的重点之一。
    // Keyboard.current 由 Input System 底层驱动，不受这两个开关影响，所以仍然收得到。
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

            Debug.Log("[EncounterResetDebug] === ResetAll() 开始 ===", this);
            EncounterScope.Current.ResetAll();
            Debug.Log("[EncounterResetDebug] === ResetAll() 结束 ===", this);
        }
    }
}
