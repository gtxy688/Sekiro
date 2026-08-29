using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBrain : BrainBase
{
    private PlayerInput playerInput;

    // Player Input 组件管理的资产实例（双方案 Auto-Switch 只作用于这个实例）
    private InputActionAsset actions;
    private InputAction moveAction;
    private InputAction attackAction;
    private InputAction jumpAction;
    private InputAction deflectAction;
    private InputAction dodgeAction;
    private InputAction healAction;
    private InputAction lockOnAction;

    // 专门用来暂存摇杆当前的输入值
    private Vector2 currentMoveInput;
    private bool attackPressed;
    private bool holdAttackTriggered;
    private bool holdThresholdChecked;
    private float attackPressedTime;
    private float acceptInputAfter;

    protected override void Awake()
    {
        base.Awake();
        playerInput = GetComponent<PlayerInput>();
        acceptInputAfter = Time.unscaledTime + 0.35f;
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
        InputRebindService.Load(actions);

        var map = actions.FindActionMap("Player");
        moveAction = actions.FindAction("Move");
        attackAction = map.FindAction("Attack");
        jumpAction = map.FindAction("Jump");
        deflectAction = map.FindAction("Deflect");
        dodgeAction = map.FindAction("Dodge");
        healAction = map.FindAction("Heal");
        lockOnAction = map.FindAction("LockOn");

        BindActions();
        GamePause.OnChanged += HandlePauseChanged;
        CursorController.BindPlayerInput(playerInput);
    }

    private void OnDestroy()
    {
        GamePause.OnChanged -= HandlePauseChanged;
        UnbindActions();
    }

    private void BindActions()
    {
        if (moveAction != null)
        {
            moveAction.performed += OnMovePerformed;
            moveAction.canceled += OnMoveCanceled;
        }
        if (attackAction != null)
        {
            attackAction.started += OnAttackStarted;
            attackAction.canceled += OnAttackCanceled;
        }
        if (jumpAction != null) jumpAction.started += OnJumpStarted;
        if (deflectAction != null)
        {
            deflectAction.started += OnDeflectStarted;
            deflectAction.canceled += OnDeflectCanceled;
        }
        if (dodgeAction != null) dodgeAction.started += OnDodgeStarted;
        if (healAction != null) healAction.started += OnHealStarted;
        if (lockOnAction != null) lockOnAction.started += OnLockOnStarted;
    }

    // InputActionAsset 可能是项目资源，场景重载/对象销毁后仍活着。
    // lambda 无法解绑，必须用具名回调在 OnDestroy 里卸掉，否则会打到已销毁的 PlayerBrain。
    private void UnbindActions()
    {
        if (moveAction != null)
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.canceled -= OnMoveCanceled;
        }
        if (attackAction != null)
        {
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
        }
        if (jumpAction != null) jumpAction.started -= OnJumpStarted;
        if (deflectAction != null)
        {
            deflectAction.started -= OnDeflectStarted;
            deflectAction.canceled -= OnDeflectCanceled;
        }
        if (dodgeAction != null) dodgeAction.started -= OnDodgeStarted;
        if (healAction != null) healAction.started -= OnHealStarted;
        if (lockOnAction != null) lockOnAction.started -= OnLockOnStarted;
    }

    private void OnMovePerformed(InputAction.CallbackContext ctx)
    {
        currentMoveInput = ctx.ReadValue<Vector2>();
    }

    private void OnMoveCanceled(InputAction.CallbackContext _)
    {
        currentMoveInput = Vector2.zero;
    }

    private void OnAttackStarted(InputAction.CallbackContext _)
    {
        if (!CanAcceptPlayInput()) return;
        attackPressed = true;
        holdAttackTriggered = false;
        holdThresholdChecked = false;
        attackPressedTime = Time.time;
    }

    private void OnAttackCanceled(InputAction.CallbackContext _)
    {
        if (attackPressed && !holdAttackTriggered && CanAcceptPlayInput())
        {
            body.ActiveAttack = null;
            BufferCommand(new AttackCommand());
        }
        attackPressed = false;
    }

    private void OnJumpStarted(InputAction.CallbackContext _)
    {
        if (CanAcceptPlayInput()) BufferCommand(new JumpCommand());
    }

    private void OnDeflectStarted(InputAction.CallbackContext _)
    {
        if (CanAcceptPlayInput()) BufferCommand(new DeflectCommand());
    }

    private void OnDeflectCanceled(InputAction.CallbackContext _)
    {
        if (CanAcceptPlayInput()) BufferCommand(new IdleCommand());
    }

    private void OnDodgeStarted(InputAction.CallbackContext ctx)
    {
        // 进 Play / Enable 时 Input System 会把残留按键当成 started，看起来像开局垫步。
        if (!ctx.ReadValueAsButton()) return;
        if (!CanAcceptPlayInput()) return;
        BufferCommand(new DodgeCommand());
    }

    private void OnHealStarted(InputAction.CallbackContext _)
    {
        if (CanAcceptPlayInput()) BufferCommand(new HealCommand());
    }

    private void OnLockOnStarted(InputAction.CallbackContext _)
    {
        if (!CanAcceptPlayInput()) return;
        if (LockOnManager.Instance != null) LockOnManager.Instance.Toggle();
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
        // Unity 伪 null：对象已销毁时读 isActiveAndEnabled 会抛 MissingReferenceException
        if (this == null) return false;
        if (Time.unscaledTime < acceptInputAfter) return false;
        return isActiveAndEnabled && !GamePause.IsPaused && !CombatInputGate.Blocked;
    }

    protected override void Update()
    {
        if (GamePause.IsPaused) return;

        if (Time.unscaledTime < acceptInputAfter)
        {
            currentMoveInput = Vector2.zero;
            if (body != null)
                body.TryExecuteCommand(new MoveCommand(Vector2.zero));
            return;
        }

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

        // 崩解倒地：长按格挡键应在 BrokenDeflectDodgeOpenTime 到达后切入 DeflectState
        if (body.IsPostureBroken && deflectAction != null && deflectAction.IsPressed())
            BufferCommand(new DeflectCommand(), 0.15f);

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
