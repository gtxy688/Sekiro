using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public class CharacterBody : MonoBehaviour
{
    // 1. 状态机
    public StateMachine MainStateMachine { get; private set; }

    // 2. 组件引用 
    public Animator Animator { get; private set; }
    public Rigidbody Rb { get; private set; }

    // 武器的碰撞盒
    public WeaponHitbox Weapon { get; private set; }

    // 3. 移动意图：无论是手柄摇杆推的，还是 Boss AI 寻路计算的，都写到这里
    public Vector3 MoveDirection { get; set; } 
    
    // 4. 物理状态 (Body 负责检测，State 读取)
    public bool IsGrounded { get; private set; }

    [Header("环境检测设置")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Rb = GetComponent<Rigidbody>();
        
        // 实例化纯 C# 的状态机引擎
        MainStateMachine = new StateMachine();
    }

    private void Start()
    {
        // 启动状态机：直接进入最外层的主状态 (复合节点)
        MainStateMachine.ChangeState(new GroundedState(this));
    }

    private void Update()
    {
        // 1. 每帧更新物理环境感知 (例如是否接地)
        // 这样做的好处是：所有 State 只需要读取 body.IsGrounded，不需要在各自内部写射线检测
        UpdateEnvironmentalChecks();

        // 2. 驱动主状态机运行 (主状态机会自动一层层往下驱动子状态机)
        MainStateMachine.Update();
    }

    // 接收大脑 (Brain) 传来的指令
    public bool TryExecuteCommand(ICommand cmd)
    {
        // 将大脑的指令直接喂给主状态机。
        // 返回 true  表示：指令被某个状态 (父状态或子状态) 成功消耗；
        // 返回 false 表示：当前层级下的所有状态都拒收这个指令。
        return MainStateMachine.HandleCommand(cmd);
    }

    // --- 物理环境检测 ---
    private void UpdateEnvironmentalChecks()
    {
        if (groundCheckPoint != null)
        {
            // 这里用简单的球形检测举例，实战中也可以用胶囊体 Cast
            IsGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            IsGrounded = true; // 容错
        }
    }

    // --- 供 State 调用的公共方法举例 ---
    // 比如在移动状态中，需要让角色转身
    public void RotateTowards(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * speed);
        }
    }

    // 接收外界物理碰撞传来的打击
    public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint)
    {
        // // 1. 询问当前的状态：你现在能被砍吗？你在弹反吗？
        // // 这就是 HFSM 强大的地方，不需要写一堆 if(isDeflecting)
        // if (MainStateMachine.CurrentState is GroundedState ground && 
        //     ground.SubStateMachine.CurrentState is DeflectState)
        // {
        //     // 当前处于完美弹反状态！
        //     HandlePerfectParry(attacker, hitPoint);
        //     return; // 拦截伤害，直接 return
        // }

        // if (MainStateMachine.CurrentState is DodgeState)
        // {
        //     // 当前处于无敌帧（闪避状态）
        //     return; // 闪避成功，免疫伤害
        // }

        // // 2. 没防住，真挨揍了
        // TakeDamage(healthDmg, postureDmg);

        // // 3. 强制打断当前行为，切入受击硬直状态！
        // // 这属于环境/物理强制覆写，不走 Command，直接强切 HFSM
        // MainStateMachine.ChangeState(new StunnedState(this));
    }

    private void HandlePerfectParry(CharacterBody attacker, Vector3 hitPoint)
    {
        // // 增加攻击者的架势条（只狼核心机制）
        // attacker.AddPosture(20f); 
        
        // // 触发事件总线，让外部系统播放“叮”的一声打铁音效和火花特效
        // CombatEventBus.TriggerPerfectParry(hitPoint);
    }
}