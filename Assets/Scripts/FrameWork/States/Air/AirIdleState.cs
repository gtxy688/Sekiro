using UnityEngine;
public class AirIdleState : BaseState
{
    private HierarchicalState parent;
    private bool wasFalling; // 上一帧是否已进入下落（只切一次，避免每帧 CrossFade）

    public AirIdleState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        // 进空中的第一帧就按当前垂直速度决定动画：
        //   跳跃进入（velocity.y 向上/0）→ 播 Jump（起跳，上升由 Root 曲线驱动）
        //   踩空掉落进入（velocity.y 向下）→ 直接播 Fall（下落）
        wasFalling = body.Rb.velocity.y < 0f;
        body.Animator.CrossFade(wasFalling ? "Fall" : "Jump", 0.1f);
    }

    public override void OnUpdate()
    {
        // 起跳 → 过最高点（velocity.y 转负）→ 切下落动画，只切一次
        bool falling = body.Rb.velocity.y < 0f;
        if (falling && !wasFalling)
        {
            wasFalling = true;
            body.Animator.CrossFade("Fall", 0.1f);
        }

        // 空中转向（相机相对，与地面移动一致）：按住方向键时朝输入方向转身
        Vector2 inputDir = body.MoveDirection;
        if (inputDir.sqrMagnitude > 0.01f && body.Config != null)
        {
            Vector3 lookDirection = body.InputToWorldDir(inputDir);
            body.RotateYaw(lookDirection, body.Config.RotationSpeed);
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        // 空中攻击/格挡已移除（无对应动画资源）
        // 攻击/防御指令在空中不被消费，会留在缓冲池 0.2s 后自动丢弃，落地前按的不会带下来
        return false;
    }

    // 受击拦截（M17 横扫跳踩）：
    //   空中被横扫危字扫到 → 跳踩反制：涨攻击者架势 + Perfect 事件，自身不掉血
    //   （普通攻击在空中 → 放行硬吃）
    public override bool OnHitReceived(HitData hit)
    {
        if (hit.isPerilous && hit.perilousType == PerilousType.Sweep)
        {
            if (hit.attacker != null)
            {
                float gain = body.Config != null ? body.Config.MikiriPostureGain : 30f;
                hit.attacker.AccumulatePosture(gain);
                hit.attacker.ForceParryStun(); // 被踩硬直（复用被弹反硬直）
            }
            CombatEventBus.TriggerWeaponDeflected(hit.hitPoint, DeflectType.Perfect);
            CombatManager.Instance?.HitStop();
            return true;
        }
        return false;
    }
}
