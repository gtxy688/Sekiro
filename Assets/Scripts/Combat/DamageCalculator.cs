
/// <summary>
/// 防御状态枚举，用于伤害计算时判断防御倍率。
/// </summary>
public enum DefenseState
{
    /// <summary>未防御（无格挡/弹刀）</summary>
    None,

    /// <summary>普通格挡（格挡窗口内受击，减伤 60%）</summary>
    NormalBlock,

    /// <summary>完美弹刀（弹刀窗口内受击，零伤害）</summary>
    PerfectDeflect
}

/// <summary>
/// 伤害计算器 — 纯静态工具类。
/// 根据攻击数据、防御值和防御状态计算最终 HP 伤害与架势伤害。
/// 无需 MonoBehaviour，所有方法均为纯数学计算，可在 EditMode 下直接测试。
/// </summary>
public static class DamageCalculator
{
    /// <summary>普通格挡伤害倍率（减伤 60%，承受 40%）</summary>
    private const float NormalBlockDamageMultiplier = 0.4f;

    /// <summary>普通格挡架势伤害倍率（承受 30%）</summary>
    private const float NormalBlockPostureMultiplier = 0.3f;

    /// <summary>
    /// 计算最终 HP 伤害。
    /// </summary>
    /// <remarks>
    /// 计算流程：
    /// 1. 原始伤害减去防御值
    /// 2. 钳制到 ≥ 0
    /// 3. 根据防御状态应用倍率：完美弹刀 → 0，普通格挡 → ×0.4，未防御 → 全额
    /// </remarks>
    /// <param name="attack">攻击数据（包含 damage 字段）</param>
    /// <param name="defense">防御者的防御值</param>
    /// <param name="state">防御状态</param>
    /// <returns>最终 HP 伤害，始终 ≥ 0</returns>
    public static float CalculateDamage(AttackData attack, float defense, DefenseState state)
    {
        if (state == DefenseState.PerfectDeflect)
            return 0f;

        float baseDamage = attack.damage - defense;

        if (baseDamage < 0f)
            baseDamage = 0f;

        if (state == DefenseState.NormalBlock)
            baseDamage *= NormalBlockDamageMultiplier;

        return baseDamage;
    }

    /// <summary>
    /// 计算最终架势伤害。
    /// </summary>
    /// <remarks>
    /// 计算流程：
    /// 1. 完美弹刀 → 0 架势伤害
    /// 2. 普通格挡 → 架势伤害 × 0.3
    /// 3. 未防御 → 架势伤害 × 弹刀倍率加成
    /// </remarks>
    /// <param name="attack">攻击数据（包含 postureDamage 字段）</param>
    /// <param name="state">防御状态</param>
    /// <param name="deflectMultiplier">连续弹刀加成倍率（来自 DeflectSystem.GetPostureDamageMultiplier()）</param>
    /// <returns>最终架势伤害，始终 ≥ 0</returns>
    public static float CalculatePostureDamage(AttackData attack, DefenseState state, float deflectMultiplier)
    {
        if (state == DefenseState.PerfectDeflect)
            return 0f;

        if (state == DefenseState.NormalBlock)
            return attack.postureDamage * NormalBlockPostureMultiplier;

        return attack.postureDamage * deflectMultiplier;
    }
}
