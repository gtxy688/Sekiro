using UnityEngine;
public class AttackState : BaseState
{
    private HierarchicalState parent;
    private AttackConfig config; // 核心：当前状态正在使用的数据配置
    
    private float stateTimer;
    private bool hasBufferedNextHit;

    // 构造函数只接收一个光盘（配置）
    public AttackState(CharacterBody body, HierarchicalState parent, AttackConfig config) : base(body)
    {
        this.parent = parent;
        this.config = config;
    }

    public override void OnEnter()
    {
        // 空配置保护：没有 AttackConfig 的 AttackState 无意义，立刻退回 Idle
        if (config == null)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            return;
        }

        stateTimer = 0f;
        hasBufferedNextHit = false;

        body.Animator.CrossFade(config.AnimName, config.TransitionDuration);

        // M8 攻击接线：进入即绑定本招式配置并开启武器判定（伤害能打出去了）
        // 动画事件版（EnableHitbox/DisableHitbox 精确帧）后续可覆盖，入口不变
        body.EnableWeaponHit(config);

        // Boss AI 反制判定标记（M7 用，避免查状态类型）
        body.IsAttacking = true;

        // 危字攻击：发事件 → UI 弹"危"字提示（M17）
        if (config.Perilous != PerilousType.None)
        {
            CombatEventBus.TriggerPerilousAttack(config.Perilous);
        }
    }

    public override void OnUpdate()
    {
        if (config == null) return;

        stateTimer += Time.deltaTime;

        // 位移不在此处理：突进/前移完全由攻击动画的 Root 曲线驱动（全权根运动），
        // 代码只负责状态时长与连招窗口判定

        // 动作彻底结束
        if (stateTimer >= config.StateDuration)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    public override void OnExit()
    {
        // M8：退出攻击状态 → 关闭武器判定，防止判定残留
        body.DisableWeaponHit();
        body.IsAttacking = false;
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (config == null) return false;

        if (cmd is AttackCommand)
        {
            // 1. 判断策划有没有配置下一段连招
            if (config.NextCombo != null)
            {
                // 2. 判断是否在连招窗口期内
                if (stateTimer >= config.ComboWindowStart && stateTimer <= config.ComboWindowEnd)
                {
                    if (!hasBufferedNextHit)
                    {
                        hasBufferedNextHit = true;
                        
                        // 神级闭环：把下一段的配置塞给一个新的 ActionState！
                        parent.SubStateMachine.ChangeState(new AttackState(body, parent, config.NextCombo));
                        return true;
                    }
                }
            }
            return false; 
        }
        
        return false;
    }
}
