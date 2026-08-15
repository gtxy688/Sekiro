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

        // 空中转向（可选）：按住方向键时朝输入方向转身，位移由动画 Root 驱动
        Vector2 inputDir = body.MoveDirection;
        if (inputDir.sqrMagnitude > 0.01f && body.Config != null)
        {
            Vector3 lookDirection = new Vector3(inputDir.x, 0, inputDir.y);
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            body.transform.rotation = Quaternion.RotateTowards(
                body.transform.rotation, targetRotation, body.Config.RotationSpeed * Time.deltaTime);
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        // 空中攻击/格挡已移除（无对应动画资源）
        // 攻击/防御指令在空中不被消费，会留在缓冲池 0.2s 后自动丢弃，落地前按的不会带下来
        return false;
    }
}
