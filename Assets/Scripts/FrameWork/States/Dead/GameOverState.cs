using UnityEngine;
using UnityEngine.SceneManagement;

// 游戏结束状态（M14）：真死。按攻击键重开当前场景（策划案：按键重开/重新加载场景）
public class GameOverState : BaseState
{
    private HierarchicalState parent;

    public GameOverState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        // 倒地动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Death", 0.1f);
    }

    public override void OnUpdate() { }

    // 按攻击键 → 重新加载场景
    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is AttackCommand)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return true;
        }
        return true; // 其余命令全吞
    }
}
