using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBrain : BrainBase
{
    private PlayerInput playerInput;

    // Player Input 组件管理的资产实例（双方案 Auto-Switch 只作用于这个实例）
    private InputActionAsset actions;
    private InputAction moveAction;
    private InputAction attackAction;

    // 专门用来暂存摇杆当前的输入值
    private Vector2 currentMoveInput;
    private bool attackPressed;
    private bool holdAttackTriggered;
    private bool holdThresholdChecked;
    private float attackPressedTime;

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
        attackAction = map.FindAction("Attack");

        // A. 绑定连续输入 (摇杆移动)
        // performed: 摇杆被推时持续触发
        moveAction.performed += ctx => currentMoveInput = ctx.ReadValue<Vector2>();
        // canceled: 摇杆松开时触发归零
        moveAction.canceled += _ => currentMoveInput = Vector2.zero;

        // B. 绑定离散输入 (动作按键，带有输入缓冲)
        // started: 按下按键的第一帧。我们生成对应的 Command 并丢进基类的缓冲池
        attackAction.started += _ =>
        {
            if (!CanAcceptPlayInput()) return;
            attackPressed = true;
            holdAttackTriggered = false;
            holdThresholdChecked = false;
            attackPressedTime = Time.time;
        };
        attackAction.canceled += _ =>
        {
            if (attackPressed && !holdAttackTriggered && CanAcceptPlayInput())
            {
                body.ActiveAttack = null;
                BufferCommand(new AttackCommand());
            }
            attackPressed = false;
        };
        map.FindAction("Jump").started += _ =>
        {
            if (CanAcceptPlayInput()) BufferCommand(new JumpCommand());
        };
        map.FindAction("Deflect").started += _ =>
        {
            if (CanAcceptPlayInput()) BufferCommand(new DeflectCommand());
        };
        // 松手时发 IdleCommand，让 DeflectState 退出回待机（防御按住不放的语义）
        map.FindAction("Deflect").canceled += _ =>
        {
            if (CanAcceptPlayInput()) BufferCommand(new IdleCommand());
        };
        map.FindAction("Dodge").started += _ =>
        {
            if (CanAcceptPlayInput()) BufferCommand(new DodgeCommand());
        };
        map.FindAction("Heal").started += _ =>
        {
            if (CanAcceptPlayInput()) BufferCommand(new HealCommand());
        };
        // M11：锁定/解锁（单 Boss）
        map.FindAction("LockOn").started += _ =>
        {
            if (!CanAcceptPlayInput()) return;
            if (LockOnManager.Instance != null) LockOnManager.Instance.Toggle();
        };

        GamePause.OnChanged += HandlePauseChanged;
    }

    private void OnDestroy()
    {
        GamePause.OnChanged -= HandlePauseChanged;
    }

    private void HandlePauseChanged(bool paused)
    {
        if (!paused) return;
        attackPressed = false;
        holdAttackTriggered = false;
        holdThresholdChecked = false;
        currentMoveInput = Vector2.zero;
    }

    private bool CanAcceptPlayInput()
    {
        return isActiveAndEnabled && !GamePause.IsPaused && !CombatInputGate.Blocked;
    }

    protected override void Update()
    {
        if (GamePause.IsPaused) return;

        // 胜利结算：丢掉预输入，持续发零移动，避免还停在走路动画里
        if (CombatInputGate.Blocked)
        {
            attackPressed = false;
            holdAttackTriggered = false;
            holdThresholdChecked = false;
            currentMoveInput = Vector2.zero;
            body.TryExecuteCommand(new MoveCommand(Vector2.zero));
            return;
        }

        ProcessHeldAttack();

        if (moveAction != null)
        {
            Vector2 rawInput = moveAction.ReadValue<Vector2>();
            currentMoveInput = rawInput.sqrMagnitude < 0.01f ? Vector2.zero : rawInput;
            // 先写入本帧方向。垫步从缓冲落地时 OnEnter 才能判断「有没有按方向键」。
            body.MoveDirection = currentMoveInput;
        }

        // 1. 调用基类的 Update，让它去处理缓冲池里的 攻击、弹反、跳跃 指令
        base.Update();

        // 2. 独立处理移动指令（连绵不断的意图，不走缓冲）
        if (moveAction == null) return;

        body.TryExecuteCommand(new MoveCommand(currentMoveInput));
    }

    private void ProcessHeldAttack()
    {
        if (!attackPressed || holdAttackTriggered || holdThresholdChecked) return;

        float threshold = body.Config != null
            ? body.Config.AttackHoldDuration
            : 0.3f;
        if (Time.time - attackPressedTime < threshold) return;

        holdThresholdChecked = true;
        if (body.ThrustAttack == null)
        {
            Debug.LogError($"{body.name} 未配置 ThrustAttack，松开攻击键后回退普通攻击。");
            return;
        }

        holdAttackTriggered = true;
        body.ActiveAttack = body.ThrustAttack;
        BufferCommand(new AttackCommand());
    }

    // 启用与禁用 InputSystem
    // 注意 OnEnable 在 Start 之前执行，此时 actions 还没拿到，所以要做空判断；
    // 挂 Player Input 组件时由其 Auto Enable Inputs 负责启用
    private void OnEnable()
    {
        // 挂了 Player Input 时由它管资产启用，这里再 Enable 会把所有 Map 一起打开
        if (playerInput != null) return;
        if (actions != null) actions.Enable();
    }

    private void OnDisable()
    {
        attackPressed = false;
        holdAttackTriggered = false;
        holdThresholdChecked = false;
        if (playerInput != null) return;
        if (actions != null) actions.Disable();
    }
}