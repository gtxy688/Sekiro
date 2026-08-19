using UnityEngine;
using UnityEngine.SceneManagement;

// 游戏结束（M14）：真死。未倒地则先 Dead 再 Deading；已躺着则直接 Deading。
// 按攻击键重开当前场景
public class GameOverState : BaseState
{
    private HierarchicalState parent;
    private readonly bool alreadyDowned;
    private float fallTimer;
    private float fallDuration = 1.2f;
    private bool lying;

    public GameOverState(CharacterBody body, HierarchicalState parent, bool alreadyDowned = false) : base(body)
    {
        this.parent = parent;
        this.alreadyDowned = alreadyDowned;
    }

    public override void OnEnter()
    {
        fallTimer = 0f;
        if (alreadyDowned)
        {
            lying = true;
            body.Animator.CrossFade("Deading", 0.05f);
        }
        else
        {
            lying = false;
            body.Animator.CrossFade("Dead", 0.1f);
        }
    }

    public override void OnUpdate()
    {
        if (lying) return;

        fallTimer += Time.deltaTime;
        var info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if ((AnimUtil.IsPlaying(info, "Dead") && info.normalizedTime >= 0.95f) || fallTimer >= fallDuration)
        {
            lying = true;
            body.Animator.CrossFade("Deading", 0.05f);
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is AttackCommand)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            return true;
        }
        return true;
    }
}
