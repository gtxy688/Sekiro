using UnityEngine;
// 1. 指令接口
public interface ICommand { }

// 受击语境（受击动画选择接口）：同一角色被打时，按"当时在干什么"分不同受击表现。
// 用户决策：动画名映射留在 CharacterConfig（SO），留空 = 回退普通受击，逐级细化。
public enum HurtContext
{
    Normal,      // 裸吃普通攻击
    Heavy,       // 强力招式（击退更强/击飞）
    Guard,       // 格挡中受击（小硬直）
    Deflected    // 被完美弹反后的硬直（攻击者播）
}

// 受击数据（值类型）：由 CombatManager / Hitbox 打包传给 ReceiveHit
// 状态机用 OnHitReceived 查询"当前在防御吗/垫步吗/受击中吗"，再决定是否拦截
public struct HitData
{
    public CharacterBody attacker;    // 攻击者
    public int healthDmg;             // 血量伤害
    public float postureDmg;          // 架势伤害
    public Vector3 hitPoint;          // 命中点（打铁火花/音效定位）
    public bool isPerilous;           // 是否危字攻击（M17）
    public PerilousType perilousType; // 危字类型（M17）
    public float knockback;           // 击退强度（0=普通受击，>0 触发 Heavy 受击表现）
}

public struct IdleCommand : ICommand { }
public struct MoveCommand : ICommand
{
    public Vector2 Direction; 
    public MoveCommand(Vector2 dir) { Direction = dir; }
}
public struct JumpCommand : ICommand { }

public struct AttackCommand : ICommand{ }

public struct DeflectCommand : ICommand{ }

// M6 新增：葫芦 / 锁定 / 闪避
public struct HealCommand : ICommand { }
public struct LockOnCommand : ICommand { }
public struct DodgeCommand : ICommand { }