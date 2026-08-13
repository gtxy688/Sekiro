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
    }

    public override void OnUpdate()
    {
        if (config == null) return;

        stateTimer += Time.deltaTime;

        // 动作彻底结束
        if (stateTimer >= config.StateDuration)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
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