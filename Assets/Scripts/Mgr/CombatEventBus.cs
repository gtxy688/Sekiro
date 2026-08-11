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
    // 1. 定义事件 (公告栏上留出贴条子的地方)
    
    // 接收两个参数 (位置, 防御类型)
    public static event Action<Vector3, DeflectType> OnWeaponDeflected;
    
    // 角色受伤事件（参数：受击者，伤害值，剩余血量）
    public static event Action<CharacterBody, int, int> OnTakeDamage;

    // 3. 修改触发器
    public static void TriggerWeaponDeflected(Vector3 hitPoint, DeflectType type)
    {
        OnWeaponDeflected?.Invoke(hitPoint, type);
    }

    public static void TriggerTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        OnTakeDamage?.Invoke(victim, dmg, currentHp);
    }
}