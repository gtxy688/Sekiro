using UnityEngine;
using UnityEngine.InputSystem;

using ARPG.Combat;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.Player
{

    [RequireComponent(typeof(PlayerInput))]
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
        private bool callbacksBound;

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
            // 始终使用 PlayerInput 管理的实例，让设备与控制方案切换作用于同一份输入。
            actions = playerInput.actions;
            InputRebindService.Load(actions);

            var map = actions.FindActionMap("Player");
            moveAction = map.FindAction("Move");
            attackAction = map.FindAction("Attack");
            jumpAction = map.FindAction("Jump");
            deflectAction = map.FindAction("Deflect");
            dodgeAction = map.FindAction("Dodge");
            healAction = map.FindAction("Heal");
            lockOnAction = map.FindAction("LockOn");

            BindActions();
            callbacksBound = true;
            GamePause.OnChanged += HandlePauseChanged;
            CursorController.BindPlayerInput(playerInput);
        }

        private void OnDestroy()
        {
            GamePause.OnChanged -= HandlePauseChanged;
            // 对象可能在 Start 前销毁，此时还没有输入订阅。
            if (callbacksBound) UnbindActions();
        }

        private void BindActions()
        {
            moveAction.performed += OnMovePerformed;
            moveAction.canceled += OnMoveCanceled;
            attackAction.started += OnAttackStarted;
            attackAction.canceled += OnAttackCanceled;
            jumpAction.started += OnJumpStarted;
            deflectAction.started += OnDeflectStarted;
            deflectAction.canceled += OnDeflectCanceled;
            dodgeAction.started += OnDodgeStarted;
            healAction.started += OnHealStarted;
            lockOnAction.started += OnLockOnStarted;
        }

        // InputActionAsset 可能是项目资源，场景重载/对象销毁后仍活着。
        // lambda 无法解绑，必须用具名回调在 OnDestroy 里卸掉，否则会打到已销毁的 PlayerBrain。
        private void UnbindActions()
        {
            moveAction.performed -= OnMovePerformed;
            moveAction.canceled -= OnMoveCanceled;
            attackAction.started -= OnAttackStarted;
            attackAction.canceled -= OnAttackCanceled;
            jumpAction.started -= OnJumpStarted;
            deflectAction.started -= OnDeflectStarted;
            deflectAction.canceled -= OnDeflectCanceled;
            dodgeAction.started -= OnDodgeStarted;
            healAction.started -= OnHealStarted;
            lockOnAction.started -= OnLockOnStarted;
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
            LogTimingInput("AttackPressed");

            // 忍杀意图必须在按键按下这一刻确定。
            // 这样破防前已经开始的普通攻击，即使在松键/缓冲期间打崩 Boss，
            // 也只会继续作为 AttackCommand，不能被状态机改解释成忍杀。
            if (CombatManager.Instance.HasAvailableFinisher(body))
            {
                attackPressed = false;
                holdAttackTriggered = false;
                holdThresholdChecked = true;
                BufferCommand(new FinisherCommand());
                return;
            }

            attackPressed = true;
            holdAttackTriggered = false;
            holdThresholdChecked = false;
            attackPressedTime = Time.time;
        }

        private void OnAttackCanceled(InputAction.CallbackContext _)
        {
            LogTimingInput("AttackReleased");
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
            if (!CanAcceptPlayInput()) return;

            // 只有真实物理按下生成新的按下身份：缓冲重试、状态重入都沿用同一个 PressId，
            // 身体侧据此保证"同一次按下只开一次弹反窗口"。
            int pressId = body.BeginDeflectPress();
            BufferCommand(DeflectCommand.Physical(pressId));
        }

        private void OnDeflectCanceled(InputAction.CallbackContext _)
        {
            LogTimingInput("DeflectReleased");

            // 松开不再用 IdleCommand 覆盖尚未消费的防御按下：释放记录挂在 PressId 上，
            // 由防御状态按窗口到期决定收刀还是继续格挡（短点按保留窗口）。
            body.NotifyDeflectReleased();
        }

        private void LogTimingInput(string action)
        {
            if (body == null || !body.EnableStateDebugLog) return;
            Debug.Log(
                $"[CombatTiming][{action}] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                $"状态={body.CurrentStatePath}，攻击按下标志={attackPressed}，" +
                $"长按攻击已触发={holdAttackTriggered}", body);
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
            LockOnManager.Instance.Toggle();
        }

        private void HandlePauseChanged(bool paused)
        {
            if (!paused) return;
            attackPressed = false;
            holdAttackTriggered = false;
            holdThresholdChecked = false;
            currentMoveInput = Vector2.zero;
            ClearDeflectInput();
        }

        // 输入被暂停 / 结算 / 生命周期阻断：清掉防御按住状态与待处理的防御命令。
        // 状态机侧的未完成尝试按生命周期取消（不记空按）；恢复输入后必须由新的物理按下重新开始。
        private void ClearDeflectInput()
        {
            if (body == null) return;
            body.ClearDeflectInput();
            DropBufferedCommand<DeflectCommand>();
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
                ClearDeflectInput();
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
                ClearDeflectInput();
                body.TryExecuteCommand(new MoveCommand(Vector2.zero));
                return;
            }

            ProcessHeldAttack();

            Vector2 rawInput = moveAction.ReadValue<Vector2>();
            currentMoveInput = rawInput.sqrMagnitude < 0.01f ? Vector2.zero : rawInput;
            // 先写入本帧方向。垫步从缓冲落地时 OnEnter 才能判断「有没有按方向键」。
            body.MoveDirection = currentMoveInput;

            // 1. 调用基类的 Update，让它去处理缓冲池里的 攻击、弹反、跳跃 指令
            base.Update();

            // 崩解倒地：长按格挡键应在 BrokenDeflectDodgeOpenTime 到达后切入 DeflectState。
            // 这是系统回防：只恢复普通格挡，不开新的完美弹反窗口、不计空按，也不伪造按键身份。
            // 玩家真实按下仍留在缓冲里优先被接受（否则会被这条请求顶掉，白丢一次弹反窗口）。
            if (body.IsPostureBroken && deflectAction.IsPressed()
                && !HasBufferedCommand<DeflectCommand>())
                BufferCommand(DeflectCommand.System(), 0.15f);

            // 2. 独立处理移动指令（连绵不断的意图，不走缓冲）
            body.TryExecuteCommand(new MoveCommand(currentMoveInput));
        }

        private void ProcessHeldAttack()
        {
            if (!attackPressed || holdAttackTriggered || holdThresholdChecked) return;

            float threshold = body.Config.AttackHoldDuration;
            if (Time.time - attackPressedTime < threshold) return;

            holdThresholdChecked = true;
            holdAttackTriggered = true;
            body.ActiveAttack = body.ThrustAttack;
            BufferCommand(new AttackCommand());
        }

        private void OnDisable()
        {
            attackPressed = false;
            holdAttackTriggered = false;
            holdThresholdChecked = false;
            // 对象停用属于生命周期阻断：未完成尝试取消（不记空按），残留命令一并丢弃。
            ClearDeflectInput();
        }
    }

}
