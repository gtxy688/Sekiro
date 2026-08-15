using UnityEngine;
// 1. 指令接口
public interface ICommand { }

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