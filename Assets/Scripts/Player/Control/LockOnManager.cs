using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork.Body;
using ARPG.Mgr;
namespace ARPG.Player
{

    // 管理玩家对当前 Boss 的锁定状态，通过事件通知准星和相机。
    public class LockOnManager : MonoBehaviour
    {
        public static LockOnManager Instance { get; private set; }

        public bool IsLockedOn { get; private set; }
        public Transform Target => CurrentBoss != null ? CurrentBoss.transform : null;

        private CharacterBody CurrentBoss => CombatManager.Instance.BossRef;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // 在锁定当前 Boss 和自由视角之间切换，不负责搜索或切换目标。
        public void Toggle()
        {
            if (IsLockedOn)
            {
                IsLockedOn = false;
                CombatEventBus.TriggerLockOnChanged(false);
                return;
            }
            if (CurrentBoss != null && CurrentBoss.CurrentHP > 0)
            {
                IsLockedOn = true;
                CombatEventBus.TriggerLockOnChanged(true);
            }
        }

        private void Update()
        {
            // 目标消失或死亡时自动解除锁定。
            if (IsLockedOn && (CurrentBoss == null || CurrentBoss.CurrentHP <= 0))
            {
                IsLockedOn = false;
                CombatEventBus.TriggerLockOnChanged(false);
            }
        }
    }

}
