using System;
using UnityEngine;

/// <summary>
/// Boss 攻击命中事件 — 由 BossAttackState 触发，PlayerStateMachineDriver 订阅。
/// 承载完整攻击数据，支持弹刀判定和伤害计算。
/// 定义在 Combat 层（可同时被 Player 和 Boss 引用）以避免 Core 层的循环依赖。
/// </summary>
public static class BossAttackEvents
{
    /// <summary>
    /// Boss 攻击命中玩家时触发。
    /// </summary>
    public static event Action<AttackData, Vector3, Vector3> OnBossAttackHitPlayer;

    /// <summary>
    /// 触发事件。
    /// </summary>
    /// <param name="attack">攻击数据</param>
    /// <param name="attackDir">攻击方向（攻击者 → 玩家）</param>
    /// <param name="playerForward">玩家正面朝向</param>
    public static void RaiseBossAttackHitPlayer(AttackData attack, Vector3 attackDir, Vector3 playerForward)
    {
        OnBossAttackHitPlayer?.Invoke(attack, attackDir, playerForward);
    }
}
