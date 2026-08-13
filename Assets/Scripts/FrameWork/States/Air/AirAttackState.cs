using UnityEngine;
public class AirAttackState : BaseState
{
    private HierarchicalState parent;
    private AttackConfig config; // 当前招式的数据配置
    private float stateTimer;

    public AirAttackState(CharacterBody body, HierarchicalState parent, AttackConfig config) : base(body)
    {
        this.parent = parent;
        this.config = config;
    }

    public override void OnEnter()
    {
        // 空配置保护：没有 AttackConfig 的空中攻击无意义，立刻退回 AirIdleState
        if (config == null)
        {
            parent.SubStateMachine.ChangeState(new AirIdleState(body, parent));
            return;
        }

        stateTimer = 0f;
        body.Animator.CrossFade(config.AnimName, config.TransitionDuration);
    }

    public override void OnUpdate()
    {
        if (config == null) return;

        stateTimer += Time.deltaTime;

        // 空中动作结束，回 AirIdleState 继续下落
        if (stateTimer >= config.StateDuration)
        {
            parent.SubStateMachine.ChangeState(new AirIdleState(body, parent));
        }
    }
}