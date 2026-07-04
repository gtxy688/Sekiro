using Sekiro.Player.Movement;
using UnityEngine;

namespace Sekiro.Camera
{
    /// <summary>
    /// 相机控制器，支持自由跟随和锁定两种模式。纯数学逻辑抽取为 static 方法，方便 EditMode 单元测试。
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        #region 参数配置

        [Header("跟随参数")]
        [Tooltip("跟随距离")]
        [SerializeField]
        private float _followDistance = 5f;

        [Tooltip("跟随高度偏移")]
        [SerializeField]
        private float _followHeight = 2f;

        [Tooltip("跟随平滑速度")]
        [SerializeField]
        private float _followSmooth = 5f;

        [Header("鼠标控制")]
        [Tooltip("鼠标灵敏度")]
        [SerializeField]
        private float _mouseSensitivity = 3f;

        [Header("垂直角度限制")]
        [Tooltip("最小垂直角度（度）")]
        [SerializeField]
        private float _minVerticalAngle = -20f;

        [Tooltip("最大垂直角度（度）")]
        [SerializeField]
        private float _maxVerticalAngle = 60f;

        [Header("锁定检测")]
        [Tooltip("锁定检测半径")]
        [SerializeField]
        private float _lockOnRadius = 30f;

        [Tooltip("锁定目标层级")]
        [SerializeField]
        private LayerMask _enemyLayer;

        #endregion

        #region 组件引用

        private Transform _playerTransform;
        private Transform _cameraTransform;
        private PlayerController _playerController;

        #endregion

        #region 运行时状态

        private float _currentYaw;
        private float _currentPitch = 15f;
        private Transform _lockOnTarget;
        private bool _isLockedOn;

        #endregion

        #region 公开属性

        /// <summary>
        /// 是否处于锁定模式
        /// </summary>
        public bool IsLockedOn => _isLockedOn;

        /// <summary>
        /// 当前锁定目标，为 null 表示非锁定模式
        /// </summary>
        public Transform CurrentLockOnTarget => _lockOnTarget;

        #endregion

        #region 生命周期

        /// <summary>
        /// 初始化组件引用和初始旋转角度
        /// </summary>
        private void Awake()
        {
            _cameraTransform = transform;
            _playerController = FindObjectOfType<PlayerController>();
            if (_playerController != null)
            {
                _playerTransform = _playerController.transform;
            }
        }

        /// <summary>
        /// 根据当前模式和输入更新相机位置和旋转
        /// </summary>
        private void LateUpdate()
        {
            if (_playerTransform == null) return;

            // 处理锁定切换输入
            HandleLockOnToggle();

            Vector3 desiredPosition;

            if (_isLockedOn && _lockOnTarget != null)
            {
                desiredPosition = UpdateLockedOnCamera();
            }
            else
            {
                desiredPosition = UpdateFreeLookCamera();
            }

            // 平滑跟随
            _cameraTransform.position = SmoothFollow(
                _cameraTransform.position, desiredPosition, _followSmooth, Time.deltaTime);
        }

        #endregion

        #region 锁定切换

        /// <summary>
        /// 处理锁定切换输入（鼠标中键）
        /// </summary>
        private void HandleLockOnToggle()
        {
            if (!UnityEngine.Input.GetMouseButtonDown(2)) return;

            if (_isLockedOn)
            {
                Unlock();
            }
            else
            {
                TryLockOn();
            }
        }

        /// <summary>
        /// 尝试锁定最近的敌人目标
        /// </summary>
        private void TryLockOn()
        {
            Collider[] hits = Physics.OverlapSphere(_playerTransform.position, _lockOnRadius, _enemyLayer);
            if (hits.Length == 0) return;

            Transform nearest = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < hits.Length; i++)
            {
                float dist = Vector3.Distance(_playerTransform.position, hits[i].transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = hits[i].transform;
                }
            }

            if (nearest != null)
            {
                _lockOnTarget = nearest;
                _isLockedOn = true;
                _playerController.LockOnTarget = nearest;
            }
        }

        /// <summary>
        /// 解除锁定
        /// </summary>
        private void Unlock()
        {
            _isLockedOn = false;
            _lockOnTarget = null;
            if (_playerController != null)
            {
                _playerController.LockOnTarget = null;
            }
        }

        #endregion

        #region 自由跟随模式

        /// <summary>
        /// 更新自由跟随模式下的相机，读取鼠标输入并计算位置
        /// </summary>
        /// <returns>期望的相机世界位置</returns>
        private Vector3 UpdateFreeLookCamera()
        {
            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

            _currentYaw += mouseX * _mouseSensitivity;
            _currentPitch -= mouseY * _mouseSensitivity;
            _currentPitch = ClampVerticalAngle(_currentPitch, _minVerticalAngle, _maxVerticalAngle);

            return CalculateFollowPosition(
                _playerTransform.position, _followDistance, _followHeight,
                _currentYaw, _currentPitch);
        }

        #endregion

        #region 锁定模式

        /// <summary>
        /// 更新锁定模式下的相机，面向锁定目标并允许鼠标调整视角
        /// </summary>
        /// <returns>期望的相机世界位置</returns>
        private Vector3 UpdateLockedOnCamera()
        {
            // 检测目标是否仍然存在
            if (_lockOnTarget == null)
            {
                Unlock();
                return _playerTransform.position;
            }

            // 鼠标水平输入调整 Yaw，垂直输入调整 Pitch
            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");
            _currentYaw += mouseX * _mouseSensitivity;
            _currentPitch -= mouseY * _mouseSensitivity;
            _currentPitch = ClampVerticalAngle(_currentPitch, _minVerticalAngle, _maxVerticalAngle);

            // 计算相机位置（围绕玩家，受鼠标影响）
            Vector3 cameraPos = CalculateFollowPosition(
                _playerTransform.position, _followDistance, _followHeight,
                _currentYaw, _currentPitch);

            // 相机始终看向锁定目标
            Quaternion lookRotation = CalculateLookAtRotation(cameraPos, _lockOnTarget.position);
            _cameraTransform.rotation = lookRotation;

            return cameraPos;
        }

        #endregion

        #region 纯数学逻辑（static，可测试）

        /// <summary>
        /// 计算相机跟随位置（目标位置 + 距离 + 高度 + 旋转），纯数学无组件依赖
        /// </summary>
        public static Vector3 CalculateFollowPosition(
            Vector3 targetPos, float distance, float height, float yaw, float pitch)
        {
            float yawRad = yaw * Mathf.Deg2Rad;
            float pitchRad = pitch * Mathf.Deg2Rad;

            float horizontalDist = distance * Mathf.Cos(pitchRad);
            float verticalOffset = distance * Mathf.Sin(pitchRad);

            float offsetX = horizontalDist * Mathf.Sin(yawRad);
            float offsetZ = horizontalDist * Mathf.Cos(yawRad);

            return new Vector3(
                targetPos.x - offsetX,
                targetPos.y + height + verticalOffset,
                targetPos.z - offsetZ);
        }

        /// <summary>限制垂直角度在 [min, max] 范围内</summary>
        public static float ClampVerticalAngle(float angle, float min, float max)
        {
            return Mathf.Clamp(angle, min, max);
        }

        /// <summary>计算相机朝向目标的旋转四元数</summary>
        public static Quaternion CalculateLookAtRotation(Vector3 cameraPos, Vector3 targetPos)
        {
            Vector3 direction = (targetPos - cameraPos).normalized;
            if (direction.sqrMagnitude < 0.001f) return Quaternion.identity;
            return Quaternion.LookRotation(direction);
        }

        /// <summary>平滑跟随插值计算（Lerp）</summary>
        public static Vector3 SmoothFollow(Vector3 current, Vector3 target, float smooth, float deltaTime)
        {
            return Vector3.Lerp(current, target, smooth * deltaTime);
        }

        #endregion
    }
}
