using UnityEngine;
using UnityEngine.InputSystem;

using ARPG.Combat;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.Player
{

    // 将玩家输入转换为移动方向和动作命令；具体能否执行由角色状态机决定。
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerBrain : BrainBase
    {
        private PlayerInput playerInput;

        // 使用 PlayerInput 管理的动作资产，保持输入与设备切换一致。
        private InputActionAsset actions;
        private InputAction moveAction;
        private InputAction attackAction;
        private InputAction jumpAction;
        private InputAction deflectAction;
        private InputAction dodgeAction;
        private InputAction healAction;
        private InputAction lockOnAction;
        private bool callbacksBound;

        // 保存键盘或摇杆的移动方向。
        private Vector2 currentMoveInput;
        // 记录本次攻击按下，用于区分普通攻击与长按突刺。
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

        // 等 PlayerInput 完成初始化后，再查找动作并订阅输入事件。
        private void Start()
        {
            actions = playerInput.actions;
            // 恢复玩家保存的改键设置。
            InputRebindService.Load(actions);

            var map = actions.FindActionMap("Player");
            moveAction = map.FindAction("Move");
            attackAction = map.FindAction("Attack");
            jumpAction = map.FindAction("Jump");
            deflectAction = map.FindAction("Deflect");
            dodgeAction = map.FindAction("Dodge");
            healAction = map.FindAction("Heal");
            lockOnAction = map.FindAction("LockOn");

            // 将各个输入动作接到对应回调。
            BindActions();
            callbacksBound = true; // 销毁时据此判断是否需要解绑。
            // 暂停时清除残留操作，避免恢复后沿用暂停前的输入。
            GamePause.OnChanged += HandlePauseChanged;
            // 让光标管理器根据键鼠 / 手柄切换调整光标。
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

        // 销毁时移除订阅，避免输入事件继续调用已销毁的对象。
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

            // 按下时决定是否忍杀，避免普通攻击在松键或缓冲期间被改成忍杀。
            if (CombatManager.Instance.HasAvailableFinisher(body))
            {
                attackPressed = false;
                holdAttackTriggered = false;
                holdThresholdChecked = true;
                BufferCommand(new FinisherCommand());
                return;
            }

            // 从按下时开始计时，后续按持续时长区分普通攻击与突刺。
            attackPressed = true;
            holdAttackTriggered = false;
            holdThresholdChecked = false;
            attackPressedTime = Time.time;
        }

        private void OnAttackCanceled(InputAction.CallbackContext _)
        {
            // 未触发长按突刺时，松键提交普通攻击。
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

            // 同一次按下共用 PressId，避免缓冲重试或状态重入重复开启弹反窗口。
            int pressId = body.BeginDeflectPress();
            BufferCommand(DeflectCommand.Physical(pressId));
        }

        private void OnDeflectCanceled(InputAction.CallbackContext _)
        {
            // 只记录松键，收刀由防御状态决定，避免短按提前关闭弹反窗口。
            body.NotifyDeflectReleased();
        }

        private void OnDodgeStarted(InputAction.CallbackContext ctx)
        {
            // 只接受按钮处于按下状态的回调。
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

        // 中断操作时清除防御状态与缓冲命令，恢复后需重新按下防御。
        private void ClearDeflectInput()
        {
            if (body == null) return;
            body.ClearDeflectInput();
            DropBufferedCommand<DeflectCommand>();
        }

        // 统一限制动作回调：组件需启用，且不在入场等待、暂停或结算期间。
        private bool CanAcceptPlayInput()
        {
            // Unity 对象销毁后，不能再读取 isActiveAndEnabled。
            if (this == null) return false;
            if (Time.unscaledTime < acceptInputAfter) return false;
            return isActiveAndEnabled && !GamePause.IsPaused && !CombatInputGate.Blocked;
        }

        protected override void Update()
        {
            if (GamePause.IsPaused) return;

            // 入场等待期间保持零移动，避免接收进入游戏前的残留操作。
            if (Time.unscaledTime < acceptInputAfter)
            {
                currentMoveInput = Vector2.zero;
                ClearDeflectInput();
                body.TryExecuteCommand(new MoveCommand(Vector2.zero));
                return;
            }

            // 结算期间清除操作并发送零移动，让角色停止移动。
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
            // 忽略微小输入，避免摇杆漂移造成角色移动。
            currentMoveInput = rawInput.sqrMagnitude < 0.01f ? Vector2.zero : rawInput;
            // 先更新方向，再消费缓冲，让垫步进入状态时能读取本帧方向。
            body.MoveDirection = currentMoveInput;

            // 由 BrainBase 尝试执行缓冲中的攻击、防御、跳跃等离散动作。
            base.Update();

            // 破防后按住防御可请求恢复普通格挡，不开启完美弹反窗口。
            // 玩家按下的防御命令优先，系统请求不能覆盖它。
            if (body.IsPostureBroken && deflectAction.IsPressed()
                && !HasBufferedCommand<DeflectCommand>())
                BufferCommand(DeflectCommand.System(), 0.15f);

            // 移动是连续输入，每帧直接提交，不占用离散动作缓冲。
            body.TryExecuteCommand(new MoveCommand(currentMoveInput));
        }

        // 按住达到配置时长后请求突刺，每次按下只请求一次。
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
            // 停用时取消防御尝试，并清除残留的防御命令。
            ClearDeflectInput();
        }
    }

}
