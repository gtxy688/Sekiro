using UnityEngine;

// 命中判定中间层（单例）：Hitbox 扫到 Hurtbox → 报告这里 → 统一查全局规则 → 调 target.ReceiveHit
// 用户决策：方案 B（低耦合，全局规则集中一处），禁止 Hitbox 直接调 target.ReceiveHit
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance { get; private set; }

    [Header("拼刀参数")]
    [Tooltip("拼刀时双方架势增长系数（默认 1 = 按对方招式 PostureDamage 全额涨）")]
    public float clashPostureMultiplier = 1f;

    private void Awake()
    {
        // 单例防重
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // A 的武器扫到 B 的 Hurtbox
    public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
    {
        CharacterBody attacker = hitbox.Owner;
        CharacterBody target = hurtbox.Owner;

        // 1. 排除打到自己（双保险，Hitbox 侧已过滤）
        if (attacker == null || target == null || attacker == target) return;

        // 2. 全局规则扩展位（后续：减伤 Buff、全场无敌、友军伤害开关等）

        // 3. 伤害数据来自 AttackConfig（SO），这里只做转发（含危字标记，M17）
        if (hitbox.Config == null) return;
        target.ReceiveHit(attacker, hitbox.Config.BaseDamage, hitbox.Config.PostureDamage, hitPoint,
                          hitbox.Config.Perilous != PerilousType.None, hitbox.Config.Perilous);
    }

    // 双方 Hitbox 相交 → 拼刀：只狼里拼刀双方都涨架势，不打伤害
    public void ReportClash(Hitbox a, Hitbox b, Vector3 point)
    {
        if (a.Owner == null || b.Owner == null || a.Owner == b.Owner) return;

        // 各按对方招式的架势伤害涨架势（乘以拼刀系数）
        if (b.Config != null)
            a.Owner.AccumulatePosture(b.Config.PostureDamage * clashPostureMultiplier);
        if (a.Config != null)
            b.Owner.AccumulatePosture(a.Config.PostureDamage * clashPostureMultiplier);

        // 表现层事件：打铁音效/火花（M13/M15 订阅）
        CombatEventBus.TriggerWeaponDeflected(point, DeflectType.Normal);
    }
}
