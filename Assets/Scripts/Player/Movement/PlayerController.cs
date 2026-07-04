using Sekiro.Player.Input;
using UnityEngine;

namespace Sekiro.Player.Movement
{
    /// <summary>
    /// 玩家角色控制器，处理移动、跳跃、重力和朝向。
    /// 使用 <see cref="CharacterController"/> 进行物理移动，
    /// 通过 <see cref="InputReaderComponent"/> 读取玩家输入。
    /// </summary>
    /// <remarks>
    /// <para>移动基于摄像机方向，支持 WASD 8 方向输入。</para>
    /// <para>跳跃为一段跳，不可二段跳。落地后重置跳跃状态。</para>
    /// <para>锁定模式下，角色面朝锁定目标；非锁定模式下，面朝移动方向。</para>
    /// </remarks>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        #region 参数配置

        [Header("移动参数")]
        [Tooltip("移动速度（米/秒）")]
        [SerializeField]
        private float _moveSpeed = 5f;

        [Tooltip("旋转平滑速度（度/秒）")]
        [SerializeField]
        private float _rotateSpeed = 10f;

        [Header("跳跃参数")]
        [Tooltip("跳跃初速度（米/秒）")]
        [SerializeField]
        private float _jumpForce = 8f;

        [Header("重力参数")]
        [Tooltip("重力加速度（米/秒²），应为负值")]
        [SerializeField]
        private float _gravity = -20f;

        [Header("锁定模式")]
        [Tooltip("当前锁定目标（为 null 表示非锁定模式）")]
        [SerializeField]
        private Transform _lockOnTarget;

        #endregion

        #region 组件引用

        private CharacterController _characterController;
        private InputReaderComponent _inputReader;
        private Transform _cameraTransform;

        #endregion

        #region 运行时状态

        private float _verticalVelocity;
        private bool _hasJumped;

        #endregion

        #region 公开属性

        /// <summary>
        /// 当前锁定目标，为 null 表示非锁定模式
        /// </summary>
        public Transform LockOnTarget { get => _lockOnTarget; set => _lockOnTarget = value; }

        /// <summary>
        /// 是否已跳跃（用于外部状态查询）
        /// </summary>
        public bool HasJumped => _hasJumped;

        /// <summary>
        /// 是否在地面上
        /// </summary>
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        /// <summary>
        /// 移动速度参数（支持运行时修改）
        /// </summary>
        public float MoveSpeed { get => _moveSpeed; set => _moveSpeed = value; }

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化组件引用和缓存
        /// </summary>
        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _inputReader = GetComponent<InputReaderComponent>();
            _cameraTransform = Camera.main != null ? Camera.main.transform : null;
        }

        /// <summary>
        /// 每帧更新：处理移动、跳跃、重力、朝向
        /// </summary>
        private void Update()
        {
            if (_inputReader == null || _characterController == null) return;

            Vector2 moveInput = _inputReader.GetMoveInput();
            bool jumpPressed = _inputReader.IsJumpPressed();
            bool grounded = _characterController.isGrounded;

            // 计算移动方向
            Vector3 moveDirection = CalculateMoveDirection(moveInput);

            // 处理跳跃
            HandleJump(jumpPressed, grounded);

            // 应用重力
            ApplyGravity(grounded);

            // 执行移动
            Vector3 motion = moveDirection * _moveSpeed * Time.deltaTime;
            motion.y = _verticalVelocity * Time.deltaTime;
            _characterController.Move(motion);

            // 更新朝向
            UpdateFacing(moveDirection);
        }

        #endregion

        #region 移动逻辑

        /// <summary>
        /// 计算基于摄像机方向的移动向量（纯逻辑，可测试）
        /// </summary>
        /// <param name="input">归一化的 2D 移动输入</param>
        /// <returns>世界空间中的移动方向（Y 分量为 0）</returns>
        internal Vector3 CalculateMoveDirection(Vector2 input)
        {
            if (input.sqrMagnitude < 0.01f) return Vector3.zero;

            // 归一化输入防止斜向移动更快
            Vector2 normalizedInput = NormalizeInput(input);

            if (_cameraTransform == null)
            {
                // 无摄像机时使用世界坐标方向
                return new Vector3(normalizedInput.x, 0f, normalizedInput.y).normalized;
            }

            // 基于摄像机方向计算移动方向
            Vector3 cameraForward = _cameraTransform.forward;
            Vector3 cameraRight = _cameraTransform.right;
            cameraForward.y = 0f;
            cameraRight.y = 0f;
            cameraForward.Normalize();
            cameraRight.Normalize();

            Vector3 direction = cameraForward * normalizedInput.y + cameraRight * normalizedInput.x;
            return direction.normalized;
        }

        /// <summary>
        /// 归一化输入向量，防止斜向移动速度过快（纯逻辑，可测试）
        /// </summary>
        /// <param name="input">原始 2D 移动输入</param>
        /// <returns>归一化后的输入向量</returns>
        public static Vector2 NormalizeInput(Vector2 input)
        {
            if (input.sqrMagnitude > 1f)
            {
                return input.normalized;
            }
            return input;
        }

        #endregion

        #region 跳跃逻辑

        /// <summary>
        /// 判断是否可以跳跃（纯逻辑，可测试）
        /// </summary>
        /// <param name="isGrounded">是否在地面上</param>
        /// <param name="hasJumped">是否已执行过跳跃</param>
        /// <returns>可以跳跃返回 true</returns>
        public static bool CanJump(bool isGrounded, bool hasJumped)
        {
            return isGrounded && !hasJumped;
        }

        /// <summary>
        /// 处理跳跃输入
        /// </summary>
        private void HandleJump(bool jumpPressed, bool grounded)
        {
            // 落地重置
            if (grounded)
            {
                _hasJumped = false;
                // 贴地时给一个微小的向下速度，确保 isGrounded 持续判定
                if (_verticalVelocity < 0f)
                {
                    _verticalVelocity = -2f;
                }
            }

            // 跳跃判定
            if (jumpPressed && CanJump(grounded, _hasJumped))
            {
                _verticalVelocity = _jumpForce;
                _hasJumped = true;
            }
        }

        /// <summary>
        /// 应用重力
        /// </summary>
        private void ApplyGravity(bool grounded)
        {
            if (grounded && _verticalVelocity < 0f)
            {
                // 已在地面且向下运动，不继续加速
                return;
            }

            _verticalVelocity += _gravity * Time.deltaTime;
        }

        #endregion

        #region 朝向逻辑

        /// <summary>
        /// 计算目标朝向（纯逻辑，可测试）
        /// </summary>
        /// <param name="moveDirection">移动方向</param>
        /// <param name="lockOnTarget">锁定目标位置，为 null 表示非锁定模式</param>
        /// <param name="currentPosition">角色当前位置</param>
        /// <returns>目标旋转四元数</returns>
        public static Quaternion CalculateTargetRotation(
            Vector3 moveDirection,
            Vector3? lockOnTarget,
            Vector3 currentPosition)
        {
            Vector3 lookDirection;

            if (lockOnTarget.HasValue)
            {
                // 锁定模式：面朝锁定目标
                lookDirection = (lockOnTarget.Value - currentPosition);
                lookDirection.y = 0f;
            }
            else
            {
                // 非锁定模式：面朝移动方向
                lookDirection = moveDirection;
            }

            if (lookDirection.sqrMagnitude < 0.01f)
            {
                return Quaternion.identity;
            }

            return Quaternion.LookRotation(lookDirection.normalized);
        }

        /// <summary>
        /// 更新角色朝向（平滑旋转）
        /// </summary>
        private void UpdateFacing(Vector3 moveDirection)
        {
            Vector3? targetPos = _lockOnTarget != null ? _lockOnTarget.position : (Vector3?)null;
            Quaternion targetRotation = CalculateTargetRotation(
                moveDirection,
                targetPos,
                transform.position);

            if (targetRotation == Quaternion.identity && moveDirection.sqrMagnitude < 0.01f)
            {
                return;
            }

            // 锁定模式下始终面朝目标，即使不移动
            if (_lockOnTarget != null || moveDirection.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    _rotateSpeed * Time.deltaTime);
            }
        }

        #endregion
    }
}
