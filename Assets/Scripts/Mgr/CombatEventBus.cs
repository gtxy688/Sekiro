using UnityEngine;
using System;

// 1. 定义防御级别
public enum DeflectType
{
    Normal,  // 普通防御 (浅黄色)
    Perfect  // 完美弹反 (深黄色/橙色)
}

// 这是一个静态类，全局唯一，充当“公告板”
public static class CombatEventBus
{
    // ========== 1. 定义事件 (公告栏上留出贴条子的地方) ==========

    // 1a. 已有事件
    // 武器被格挡（位置, 防御类型）—— 打铁音效/火花
    public static event Action<Vector3, DeflectType> OnWeaponDeflected;

    // 角色受伤（受击者, 伤害值, 剩余血量）
    public static event Action<CharacterBody, int, int> OnTakeDamage;

    // 1b. 战斗数值事件（M2 触发，携带完整数据，表现层不读 CharacterBody 内部字段）
    // 血量变化（角色, 当前血量, 最大血量）
    public static event Action<CharacterBody, int, int> OnHPChanged;
    // 架势变化（角色, 当前架势, 最大架势）
    public static event Action<CharacterBody, float, float> OnPostureChanged;
    // 架势崩解（角色）—— 触发处决窗口
    public static event Action<CharacterBody> OnPostureBroken;
    // 忍杀机会显隐（目标, 是否可忍杀）—— UI 红点只订阅事件，不轮询
    public static event Action<CharacterBody, bool> OnFinisherOpportunityChanged;
    // 葫芦使用（角色, 剩余次数）
    public static event Action<CharacterBody, int> OnGourdUsed;
    // 角色死亡
    public static event Action<CharacterBody> OnDeath;
    // 复活可用（角色）—— 弹出回生提示
    public static event Action<CharacterBody> OnReviveAvailable;
    // 胜利（Boss 命数清空）—— M10 处决完最后一条命触发
    public static event Action<CharacterBody> OnVictory;
    // 清命（角色, 剩余命数）—— 忍杀灯熄灭一个（M10 UI）
    public static event Action<CharacterBody, int> OnLifeCleared;
    // 复活成功（角色）—— 隐藏回生提示（M14 UI）
    public static event Action<CharacterBody> OnRevived;
    // 锁定状态变化（是否锁定）—— 锁定点 UI + 相机模式切换（M11/M12）
    public static event Action<bool> OnLockOnChanged;

    // 1c. 战斗表现事件
    // 危字攻击（M17 触发）—— UI 弹"危"
    public static event Action<PerilousType> OnPerilousAttack;
    // 忍杀触发（位置）—— 处决音效/特效
    public static event Action<Vector3> OnFinisherTriggered;
    // 相机震动（强度）—— 弹反/崩解/处决时触发
    public static event Action<float> OnCameraShake;

    // ========== 2. 修改触发器 ==========

    public static void TriggerWeaponDeflected(Vector3 hitPoint, DeflectType type)
    {
        OnWeaponDeflected?.Invoke(hitPoint, type);
    }

    public static void TriggerTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        OnTakeDamage?.Invoke(victim, dmg, currentHp);
    }

    public static void TriggerHPChanged(CharacterBody c, int currentHp, int maxHp)
    {
        OnHPChanged?.Invoke(c, currentHp, maxHp);
    }

    public static void TriggerPostureChanged(CharacterBody c, float posture, float maxPosture)
    {
        OnPostureChanged?.Invoke(c, posture, maxPosture);
    }

    public static void TriggerPostureBroken(CharacterBody c)
    {
        OnPostureBroken?.Invoke(c);
    }

    public static void TriggerFinisherOpportunityChanged(CharacterBody target, bool available)
    {
        OnFinisherOpportunityChanged?.Invoke(target, available);
    }

    public static void TriggerGourdUsed(CharacterBody c, int remaining)
    {
        OnGourdUsed?.Invoke(c, remaining);
    }

    public static void TriggerDeath(CharacterBody c)
    {
        OnDeath?.Invoke(c);
    }

    public static void TriggerReviveAvailable(CharacterBody c)
    {
        OnReviveAvailable?.Invoke(c);
    }

    public static void TriggerVictory(CharacterBody c)
    {
        OnVictory?.Invoke(c);
    }

    public static void TriggerLifeCleared(CharacterBody c, int remainingLives)
    {
        OnLifeCleared?.Invoke(c, remainingLives);
    }

    public static void TriggerRevived(CharacterBody c)
    {
        OnRevived?.Invoke(c);
    }

    public static void TriggerLockOnChanged(bool isLocked)
    {
        OnLockOnChanged?.Invoke(isLocked);
    }

    public static void TriggerPerilousAttack(PerilousType type)
    {
        OnPerilousAttack?.Invoke(type);
    }

    public static void TriggerFinisher(Vector3 pos)
    {
        OnFinisherTriggered?.Invoke(pos);
    }

    public static void TriggerCameraShake(float intensity)
    {
        OnCameraShake?.Invoke(intensity);
    }
}