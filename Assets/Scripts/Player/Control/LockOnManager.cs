using UnityEngine;

// 锁定系统（M11）：单 Boss 战，手动按键锁定/解锁（策划案：中键锁定，不做切目标）
// 挂玩家身上。目标固定 = CombatManager.BossRef。
// M12 相机接入前：只影响角色面向（移动/攻击时面朝 Boss），相机锁定后补
public class LockOnManager : MonoBehaviour
{
    public static LockOnManager Instance { get; private set; }

    public bool IsLockedOn { get; private set; }
    public Transform Target => CurrentBoss != null ? CurrentBoss.transform : null;

    private CharacterBody CurrentBoss
    {
        get
        {
            if (CombatManager.Instance != null) return CombatManager.Instance.BossRef;
            return null;
        }
    }

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

    // 锁定键：无目标→锁定；已锁定→解锁（单 Boss 不切目标）
    public void Toggle()
    {
        if (IsLockedOn)
        {
            IsLockedOn = false;
            CombatEventBus.TriggerLockOnChanged(false); // 表现层订阅（锁定点 UI）
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
        // 目标死亡自动解锁
        if (IsLockedOn && (CurrentBoss == null || CurrentBoss.CurrentHP <= 0))
        {
            IsLockedOn = false;
            CombatEventBus.TriggerLockOnChanged(false);
        }
    }
}
