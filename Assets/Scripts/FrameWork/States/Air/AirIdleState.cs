using UnityEngine;
public class AirIdleState : BaseState
{
    private HierarchicalState parent;
    public AirIdleState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter() 
    { 
        // 播放空中下落的循环动画
        body.Animator.CrossFade("Fall_Loop", 0.1f); 
    }

    public override void OnUpdate()
    {
        // 空中可以移动（根据你的需求）
        if (body.MoveDirection.sqrMagnitude > 0.01f)
        {
            // 在空中给刚体施加微弱的横向移动力，实现空中微调
            body.Rb.AddForce(new Vector3(body.MoveDirection.x, 0, body.MoveDirection.y) * 10f);
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is AttackCommand)
        {
            // 收到相同的攻击指令，但我目前在空中,所以会切到 AirAttackState！
            parent.SubStateMachine.ChangeState(new AttackState(body, parent, null));
            return true;
        }
        
        if (cmd is DeflectCommand)
        {
            // 切入空中格挡
            parent.SubStateMachine.ChangeState(new AirDeflectState(body, parent));
            return true;
        }

        return false;
    }
}