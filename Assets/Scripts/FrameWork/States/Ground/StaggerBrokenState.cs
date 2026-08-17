using UnityEngine;

// 架势崩解硬直（M9）：
//   玩家崩解 → 击飞倒地动画，结束后架势清空（不被处决）
//   Boss 崩解 → 处决窗口（红点），期间玩家可走近按攻击键处决，超时恢复
// 由 CharacterBody.ForcePostureBroken 强切（顶层走 GroundedState 初始子状态），
// 结束由 body.RecoverFromBreak 切顶层，不依赖 parent 引用
public class StaggerBrokenState : BaseState
{
    private float timer;
    private float duration;

    public StaggerBrokenState(CharacterBody body) : base(body)
    {
        duration = body.Config != null ? body.Config.PostureBrokenDuration : 5f;
    }

    public override void OnEnter()
    {
        timer = 0f;

        // 崩解动画：独立字段（占位名 Stagger_Broken，M8 接动画前）
        string anim = body.Config != null && !string.IsNullOrEmpty(body.Config.HurtAnim_Broken)
            ? body.Config.HurtAnim_Broken : "Hurt_Ground";
        body.Animator.CrossFade(anim, 0.05f);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        // 崩解窗口结束：架势清空恢复（不扣命）
        if (timer >= duration)
        {
            body.RecoverFromBreak();
        }
    }

    // 崩解期间吞掉所有命令（Boss 无法行动，处决由玩家侧 CombatManager 触发）
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }
}
