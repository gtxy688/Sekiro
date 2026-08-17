using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBrain : BrainBase
{
    private PlayerInput playerInput;

    // Player Input 组件管理的资产实例（双方案 Auto-Switch 只作用于这个实例）
    private InputActionAsset actions;
    private InputAction moveAction;

    // 专门用来暂存摇杆当前的输入值
    private Vector2 currentMoveInput;

    protected override void Awake()
    {
        base.Awake();
        playerInput = GetComponent<PlayerInput>();
    }

    // 订阅放在 Start：Player Input 组件可能在自己 Awake 里才实例化动作资产，
    // Start 保证此时 playerInput.actions 一定就绪
    private void Start()
    {
        // 关键：使用 Player Input 组件管理的资产实例，而不是自己 new。
        // 双 Control Scheme（KeyboardMouse/Gamepad）+ Auto-Switch 只对组件管理的实例生效；
        // 自己 new 的实例不会跟着切换方案，键盘和手柄绑定会同时生效（互相干扰/漂移混入）。
        // 没挂 Player Input 组件时回退自建实例（自动切换失效，但保证不崩）。
        actions = (playerInput != null && playerInput.actions != null)
            ? playerInput.actions
            : new PlayerInputActions().asset;

        var map = actions.FindActionMap("Player");
        moveAction = actions.FindAction("Move");

        // A. 绑定连续输入 (摇杆移动)
        // performed: 摇杆被推时持续触发
        moveAction.performed += ctx => currentMoveInput = ctx.ReadValue<Vector2>();
        // canceled: 摇杆松开时触发归零
        moveAction.canceled += _ => currentMoveInput = Vector2.zero;

        // B. 绑定离散输入 (动作按键，带有输入缓冲)
        // started: 按下按键的第一帧。我们生成对应的 Command 并丢进基类的缓冲池
        map.FindAction("Attack").started += _ => BufferCommand(new AttackCommand());
        map.FindAction("Jump").started += _ => BufferCommand(new JumpCommand());
        map.FindAction("Deflect").started += _ => BufferCommand(new DeflectCommand());
        // 松手时发 IdleCommand，让 DeflectState 退出回待机（防御按住不放的语义）
        map.FindAction("Deflect").canceled += _ => BufferCommand(new IdleCommand());
        map.FindAction("Dodge").started += _ => BufferCommand(new DodgeCommand());
        map.FindAction("Heal").started += _ => BufferCommand(new HealCommand());
        // M11：锁定/解锁（单 Boss）
        map.FindAction("LockOn").started += _ =>
        {
            if (LockOnManager.Instance != null) LockOnManager.Instance.Toggle();
        };
    }

    protected override void Update()
    {
        // 1. 调用基类的 Update，让它去处理缓冲池里的 攻击、弹反、跳跃 指令
        base.Update();

        // 2. 独立处理移动指令 (连绵不断的意图)
        // 直接生成移动指令并尝试执行，不经过缓冲池。
        // 因为摇杆是连续的，当前是什么方向就发什么方向，不需要“预输入”未来的摇杆方向。
        //
        // 【关键修复】不依赖 performed/canceled 事件，每帧 ReadValue 取动作当前值：
        //   旧写法只在事件里更新值 → 松键瞬间若动作值未回零（漂移/残留），
        //   canceled 不触发，currentMoveInput 停留在残留值 → 角色松键后一直走。
        //   每帧 ReadValue 拿到的始终是真实当前值；双方案隔离后键盘/手柄互不叠加，
        //   摇杆绑定上的 StickDeadzone 把漂移压成 0，松键即回零。
        if (moveAction == null) return;

        Vector2 rawInput = moveAction.ReadValue<Vector2>();

        // 代码层死区兜底（与 MoveState 的 0.01 阈值一致，防御处理器没配的情况）
        currentMoveInput = rawInput.sqrMagnitude < 0.01f ? Vector2.zero : rawInput;

        body.TryExecuteCommand(new MoveCommand(currentMoveInput));
    }

    // 启用与禁用 InputSystem
    // 注意 OnEnable 在 Start 之前执行，此时 actions 还没拿到，所以要做空判断；
    // 挂 Player Input 组件时由其 Auto Enable Inputs 负责启用
    private void OnEnable()
    {
        if (actions != null) actions.Enable();
    }

    private void OnDisable()
    {
        if (actions != null) actions.Disable();
    }
}