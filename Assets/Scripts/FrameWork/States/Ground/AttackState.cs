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
        stateTimer = 0f;
        hasBufferedNextHit = false;

        // 无脑读取配置播放动画
        body.Animator.CrossFade(config.AnimName, config.TransitionDuration);
    }

    public override void OnUpdate()
    {
        stateTimer += Time.deltaTime;

        // 动作彻底结束
        if (stateTimer >= config.StateDuration)
        {
            // 如果是空中动作，你可以在配置里加个 bool isAirAction 来判断切回哪
            parent.SubStateMachine.ChangeState(new IdleState(body, parent)); 
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
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