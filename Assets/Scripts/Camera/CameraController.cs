using System.Collections;
using UnityEngine;
using Cinemachine;

using ARPG.Combat;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.Mgr;
using ARPG.Player;
namespace ARPG.Camera
{

    // 管理自由、锁定和处决三种机位，优先级依次升高。
    // 战斗事件负责切换，每帧更新避障虚化、跳跃突刺和重箭镜头偏移。
    public class CameraController : MonoBehaviour
    {
        [System.Serializable]
        public class FinisherCameraProfile
        {
            [Tooltip("相机后方偏移：X=左右偏向(负值偏刀刃侧/左侧), Y=高度(负值低机位仰视), Z=前后距离")]
            public Vector3 followOffset = new Vector3(-0.48f, -0.05f, -2.65f);

            [Tooltip("相机瞄准偏移（相对受害者根的高度）")]
            public Vector3 lookAtOffset = new Vector3(0f, 1.15f, 0f);

            [Tooltip("长焦视场角 FOV")]
            public float fov = 46f;

            [Tooltip("处决期间是否沿刺刀朝向微推（Dolly In）")]
            public bool enableDolly = true;

            [Tooltip("微推距离（米）")]
            public float dollyDistance = 0.4f;

            [Tooltip("微推时长（秒）")]
            public float dollyDuration = 1.5f;
        }

        [Header("虚拟相机")]
        [SerializeField] private CinemachineFreeLook freeLook;
        [SerializeField] private CinemachineVirtualCamera lockVcam;
        [SerializeField] private CinemachineVirtualCamera finisherVcam;

        [Header("跟随与代理")]
        [Tooltip("玩家根，只用来找人和给跟随点 source。相机不要直接 Follow 这里。")]
        [SerializeField] private Transform playerFollow;
        [SerializeField] private CameraFollowTarget followProxy;
        [SerializeField] private CinemachineOrbitInput orbitInput;

        [Header("切镜与优先级")]
        [Tooltip("FreeLook <-> 锁定的混合时长。太短像硬切，太长拖沓")]
        [SerializeField] private float blendTime = 0.6f;
        [SerializeField] private int freePriority = 10;
        [SerializeField] private int lockPriority = 20;
        [SerializeField] private int finisherPriority = 30;

        [Header("锁定机位（相对平滑跟随点，点已在胸口高度）")]
        [Tooltip("略偏肩后。Y 不要再加身高，跟随点已经抬到胸口")]
        [SerializeField] private Vector3 followOffset = new Vector3(0.35f, 0.2f, -3.4f);
        [Tooltip("LookAt 相对 Boss 根的胸口偏移")]
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

        [Header("处决特写机位配置（只狼式刀刃侧低机位）")]
        [Tooltip("普通地面刺杀忍杀机位（Finsher_Ground）")]
        [SerializeField] private FinisherCameraProfile groundFinisherProfile = new FinisherCameraProfile
        {
            followOffset = new Vector3(-0.48f, -0.05f, -2.65f),
            lookAtOffset = new Vector3(0f, 1.15f, 0f),
            fov = 46f,
            enableDolly = true,
            dollyDistance = 0.4f,
            dollyDuration = 1.5f
        };

        [Tooltip("弹反处决机位（Finsher_Deflect）")]
        [SerializeField] private FinisherCameraProfile deflectFinisherProfile = new FinisherCameraProfile
        {
            followOffset = new Vector3(-0.68f, -0.1f, -2.85f),
            lookAtOffset = new Vector3(0f, 1.1f, 0f),
            fov = 48f,
            enableDolly = true,
            dollyDistance = 0.35f,
            dollyDuration = 1.4f
        };

        // 识破机位从右后方略微俯视，将 Boss 正面和踩刀位置同时纳入画面。
        [Tooltip("识破踩刀处决机位（Finsher_Mikiri）")]
        [SerializeField] private FinisherCameraProfile mikiriFinisherProfile = new FinisherCameraProfile
        {
            followOffset = new Vector3(0.55f, 0.2f, -2.6f),
            lookAtOffset = new Vector3(0.1f, 0.85f, 0f),
            fov = 47f,
            enableDolly = true,
            dollyDistance = 0.38f,
            dollyDuration = 1.5f
        };

        [Header("避障配置")]
        [Tooltip("会挡住镜头的层。玩家/Boss 在 Hurtbox，不要勾进去，否则会把镜头拉进角色")]
        [SerializeField] private LayerMask obstacleLayers = 1 | (1 << 3); // Default + Ground

        [Header("避障虚化（镜头被墙挤死时把玩家淡出让位）")]
        [Tooltip("实际距离 < 期望距离 × 该比例 → 判定镜头被墙挤死，开始虚化玩家")]
        [SerializeField] private float squeezeFadeEnter = 0.45f;
        [Tooltip("迟滞恢复阈值：比例高于该值才恢复不透明，避免临界距离闪烁")]
        [SerializeField] private float squeezeFadeExit = 0.62f;

        [Header("重箭格挡/弹反镜头（下压 + 后拉，跟随玩家后滑）")]
        [Tooltip("普通格挡重箭：后拉距离（米）")]
        [SerializeField] private float arrowGuardBack = 0.55f;
        [Tooltip("普通格挡重箭：下压距离（米）")]
        [SerializeField] private float arrowGuardDown = 0.3f;
        [Tooltip("完美弹反重箭：后拉距离（米）")]
        [SerializeField] private float arrowDeflectBack = 0.35f;
        [Tooltip("完美弹反重箭：下压距离（米）")]
        [SerializeField] private float arrowDeflectDown = 0.18f;
        [Tooltip("下沉/后拉到位时间（秒）。要快，跟上打击感")]
        [SerializeField] private float arrowReactAttack = 0.12f;
        [Tooltip("整个镜头反应总时长（秒），≈ 重箭后滑时长")]
        [SerializeField] private float arrowReactDuration = 0.85f;

        [Header("JumpThrust 镜头（机位下沉仰视，LookAt 跟髋；Jump_Danger 不开）")]
        [Tooltip("机位下沉距离（米）。正数=往下，用来仰视跳起的 Boss")]
        [SerializeField] private float jumpThrustLift = 1.15f;
        [SerializeField] private float jumpThrustBack = 0.45f;
        [SerializeField] private float jumpThrustBlendIn = 0.16f;
        [SerializeField] private float jumpThrustBlendOut = 0.45f;
        [Tooltip("Boss 髋骨升高 → LookAt 上抬系数（机位不再跟着抬，否则会变成俯视）")]
        [SerializeField] private float jumpThrustHeightFollow = 1f;
        [Tooltip("LookAt 额外上抬上限（米）")]
        [SerializeField] private float jumpThrustMaxExtraLift = 5f;

        private CinemachineBrain brain;
        private Coroutine releaseLockYawCo;
        private Coroutine finisherDollyCo;
        private bool isInFinisher;

        private CameraObstacleFader obstacleFader;
        private bool obstacleSqueezed;

        private float arrowReactStart = -999f;
        private float arrowBackPeak;
        private float arrowDownPeak;
        private float lastArrowEnv;

        private bool jumpThrustActive;
        private float jumpThrustEnv;
        private float jumpThrustTarget;
        private float lastJumpEnv;
        private CharacterBody jumpThrustBoss;
        private float jumpThrustBossBaseY;
        private float jumpThrustPeakHipDy;

        private CinemachineTransposer lockTransposer;
        private CinemachineComposer lockComposer;
        private Vector3 lockBaseOffset;
        private float[] baseOrbitRadius;
        private float[] baseOrbitHeight;

        private void Awake()
        {
            InitializeCamera();
        }

        private void OnEnable()
        {
            CombatEventBus.OnLockOnChanged += HandleLockOnChanged;
            CombatEventBus.OnFinisherStarted += HandleFinisherStarted;
            CombatEventBus.OnFinisherEnded += HandleFinisherEnded;
            CombatEventBus.OnHeavyArrowDefended += HandleHeavyArrowDefended;
            CombatEventBus.OnJumpThrustCamera += HandleJumpThrustCamera;
        }

        private void OnDisable()
        {
            CombatEventBus.OnLockOnChanged -= HandleLockOnChanged;
            CombatEventBus.OnFinisherStarted -= HandleFinisherStarted;
            CombatEventBus.OnFinisherEnded -= HandleFinisherEnded;
            CombatEventBus.OnHeavyArrowDefended -= HandleHeavyArrowDefended;
            CombatEventBus.OnJumpThrustCamera -= HandleJumpThrustCamera;

            if (releaseLockYawCo != null)
            {
                StopCoroutine(releaseLockYawCo);
                releaseLockYawCo = null;
            }

            if (finisherDollyCo != null)
            {
                StopCoroutine(finisherDollyCo);
                finisherDollyCo = null;
            }
        }

        private void Start()
        {
            bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn;
            HandleLockOnChanged(locked);
            if (!locked)
                StartCoroutine(SnapBehindAfterFollowReady());
        }

        // 等待跟随代理完成首帧定位，再将自由相机转到玩家背后。
        private IEnumerator SnapBehindAfterFollowReady()
        {
            yield return null;
            if (LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn) yield break;
            SnapFreeLookBehindPlayer();
        }

        // ===== 0. 每帧驱动：避障虚化 + 重箭防御镜头 =====

        private void LateUpdate()
        {
            UpdateObstacleFade();
            UpdateJumpThrustCamera();
            UpdateArrowReaction();
        }

        private void HandleJumpThrustCamera(bool active, CharacterBody boss, float bossBaseY)
        {
            if (isInFinisher) return;
            jumpThrustActive = active;
            jumpThrustTarget = active ? 1f : 0f;
            if (active)
            {
                jumpThrustBoss = boss;
                jumpThrustBossBaseY = bossBaseY;
                jumpThrustPeakHipDy = 0f;
            }
            // 关闭时不立刻丢掉 Boss：下落跟髋还要读高度，env 到 0 再清。
        }

        // 渐变 JumpThrust 镜头权重，完全退出后释放 Boss 跟随引用。
        private void UpdateJumpThrustCamera()
        {
            if (isInFinisher) return;
            float speed = jumpThrustTarget > jumpThrustEnv ? jumpThrustBlendIn : jumpThrustBlendOut;
            jumpThrustEnv = Mathf.MoveTowards(jumpThrustEnv, jumpThrustTarget, Time.deltaTime / speed);
            if (jumpThrustTarget <= 0f && jumpThrustEnv <= 0.001f)
            {
                jumpThrustEnv = 0f;
                jumpThrustBoss = null;
                jumpThrustPeakHipDy = 0f;
            }
        }

        // 镜头被障碍挤近时淡出玩家；进入和退出使用不同阈值，避免临界距离反复闪烁。
        private void UpdateObstacleFade()
        {
            bool squeezed = false;
            if (!isInFinisher)
            {
                UnityEngine.Camera cam = UnityEngine.Camera.main;
                float desired = GetDesiredFollowDistance();
                float actual = Vector3.Distance(followProxy.transform.position, cam.transform.position);
                float ratio = actual / desired;
                squeezed = obstacleSqueezed
                    ? ratio < squeezeFadeExit   // 已虚化：回到安全比例才恢复
                    : ratio < squeezeFadeEnter; // 未虚化：压得更狠才触发
            }

            obstacleSqueezed = squeezed;
            obstacleFader.SetOccluded(squeezed);
        }

        // 用基准机位计算期望距离，避免重箭偏移影响避障比例并形成反馈。
        private float GetDesiredFollowDistance()
        {
            if (lockVcam.Priority > freeLook.Priority)
                return followOffset.magnitude;

            float y = Mathf.Clamp01(freeLook.m_YAxis.Value);
            // Cinemachine FreeLook：m_YAxis 0 = 底轨 m_Orbits[2]，0.5 = 中轨，1 = 顶轨 m_Orbits[0]
            return y <= 0.5f
                ? Mathf.Lerp(baseOrbitRadius[2], baseOrbitRadius[1], y * 2f)
                : Mathf.Lerp(baseOrbitRadius[1], baseOrbitRadius[0], (y - 0.5f) * 2f);
        }

        // 玩家挡住重箭时记录下压、后拉的峰值和起始时间。
        private void HandleHeavyArrowDefended(CharacterBody body, bool perfect)
        {
            if (isInFinisher) return;
            if (body.transform != playerFollow) return;

            arrowBackPeak = perfect ? arrowDeflectBack : arrowGuardBack;
            arrowDownPeak = perfect ? arrowDeflectDown : arrowGuardDown;
            arrowReactStart = Time.time;
        }

        // 将重箭反应和跳跃突刺偏移叠加到缓存机位，结束时恢复基准。
        private void UpdateArrowReaction()
        {
            float e = isInFinisher ? 0f : ComputeArrowEnvelope();
            float lookUp = 0f;
            float fall01 = 1f;
            if (jumpThrustEnv > 0.01f && jumpThrustBoss != null)
            {
                float hipDy = jumpThrustBoss.GetJumpFollowWorldY() - jumpThrustBossBaseY;
                if (hipDy < 0f) hipDy = 0f;
                if (hipDy > jumpThrustPeakHipDy) jumpThrustPeakHipDy = hipDy;
                // 髋从顶点往下落时，机位/后拉跟着收。顶点太低则不缩放，避免起跳瞬间被抹掉。
                if (jumpThrustPeakHipDy > 0.12f)
                    fall01 = Mathf.Clamp01(hipDy / jumpThrustPeakHipDy);
                lookUp = Mathf.Clamp(hipDy * jumpThrustHeightFollow, 0f, jumpThrustMaxExtraLift);
            }
            // 机位下沉、LookAt 跟髋：两者反向才是仰视。下落用 fall01 逐渐还原。
            float jDrop = jumpThrustLift * jumpThrustEnv * fall01;
            float jBack = jumpThrustBack * jumpThrustEnv * fall01;
            if (e <= 0f && lastArrowEnv <= 0f && jumpThrustEnv <= 0f && lastJumpEnv <= 0f) return;
            lastArrowEnv = e;
            lastJumpEnv = jumpThrustEnv;

            // 锁定机位：直接推 Transposer 偏移（Z 越负越远，Y 越低越贴地）
            lockTransposer.m_FollowOffset = lockBaseOffset
                    + new Vector3(0f, -jDrop - arrowDownPeak * e, -jBack - arrowBackPeak * e);

            lockComposer.m_TrackedObjectOffset = lookAtOffset + new Vector3(0f, lookUp * jumpThrustEnv, 0f);

            // 自由机位：推三条轨道的半径和高度
            for (int i = 0; i < 3; i++)
            {
                freeLook.m_Orbits[i].m_Radius = baseOrbitRadius[i] + jBack + arrowBackPeak * e;
                freeLook.m_Orbits[i].m_Height = baseOrbitHeight[i] - jDrop - arrowDownPeak * e;
            }
        }

        // 快速下沉到位（attack 段），再随玩家后滑结束缓回（decay 段）
        private float ComputeArrowEnvelope()
        {
            if (arrowBackPeak <= 0f && arrowDownPeak <= 0f) return 0f;

            float t = Time.time - arrowReactStart;
            float total = Mathf.Max(0.05f, arrowReactDuration);
            if (t < 0f || t >= total) return 0f;

            float attack = Mathf.Clamp(arrowReactAttack, 0.01f, total * 0.5f);
            if (t < attack)
                return Mathf.SmoothStep(0f, 1f, t / attack);
            return 1f - Mathf.SmoothStep(0f, 1f, (t - attack) / (total - attack));
        }

        // ===== 1. 锁定逻辑 =====

        private void HandleLockOnChanged(bool isLocked)
        {
            if (isInFinisher) return; // 处决中不响应普通锁定切换

            if (isLocked)
                ApplyLock();
            else
                ApplyFree();
        }

        private void ApplyLock()
        {
            Transform target = LockOnManager.Instance != null ? LockOnManager.Instance.Target : null;
            if (target == null)
            {
                ApplyFree();
                return;
            }

            if (releaseLockYawCo != null)
            {
                StopCoroutine(releaseLockYawCo);
                releaseLockYawCo = null;
            }
            lockVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;

            followProxy.SetYawTarget(target);
            lockVcam.Follow = followProxy.transform;
            lockVcam.LookAt = target;

            lockVcam.Priority = lockPriority;
            freeLook.Priority = 0;
            finisherVcam.Priority = 0;
            orbitInput.enabled = false;
        }

        private void ApplyFree()
        {
            SnapFreeLookBehindPlayer();
            freeLook.m_RecenterToTargetHeading.CancelRecentering();

            finisherVcam.Priority = 0;
            lockVcam.Priority = 0;
            freeLook.Priority = freePriority;
            orbitInput.enabled = true;

            if (releaseLockYawCo != null) StopCoroutine(releaseLockYawCo);
            releaseLockYawCo = StartCoroutine(ReleaseLockYawAfterBlend());
        }

        // 等待切镜结束再解除代理朝向，避免自由镜头混合过程中突然旋转。
        private IEnumerator ReleaseLockYawAfterBlend()
        {
            yield return null;
            float timeout = Mathf.Max(0.05f, blendTime) + 0.25f;
            float elapsed = 0f;
            while (brain.IsBlending && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            followProxy.SetYawTarget(null);
            releaseLockYawCo = null;
        }

        // 用玩家朝向计算背后机位，避免继承编辑器中主相机的任意位置。
        private void SnapFreeLookBehindPlayer()
        {
            Transform player = playerFollow;

            Vector3 fwd = player.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return;
            fwd.Normalize();

            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            if (yaw > 180f) yaw -= 360f;
            else if (yaw < -180f) yaw += 360f;
            freeLook.m_XAxis.Value = yaw;

            float y = orbitInput.YCenter;
            freeLook.m_YAxis.Value = Mathf.Clamp(y, 0.02f, 0.98f);

            float radius = Mathf.Max(1.6f, freeLook.m_Orbits[1].m_Radius);
            float height = followProxy.height;
            Vector3 pivot = player.position + Vector3.up * height;
            Vector3 camPos = pivot - fwd * radius;
            freeLook.ForceCameraPosition(camPos, Quaternion.LookRotation(fwd, Vector3.up));

            orbitInput.IgnoreLookUntil(Time.unscaledTime + 0.45f);
        }

        // ===== 处决特写 =====

        private FinisherCameraProfile GetProfileForKind(FinisherKind kind)
        {
            switch (kind)
            {
                case FinisherKind.Deflect:
                    return deflectFinisherProfile;
                case FinisherKind.Mikiri:
                    return mikiriFinisherProfile;
                default:
                    return groundFinisherProfile;
            }
        }

        private void HandleFinisherStarted(
            Vector3 hitPos,
            CharacterBody player,
            CharacterBody victim,
            FinisherKind kind)
        {
            isInFinisher = true;
            orbitInput.enabled = false;

            Transform victimT = victim.transform;

            FinisherCameraProfile profile = GetProfileForKind(kind);

            // 跟随玩家代理点，并转向被处决者。
            followProxy.SetYawTarget(victimT);
            finisherVcam.Follow = followProxy.transform;
            finisherVcam.LookAt = victimT;

            // 使用对应处决类型的机位偏移。
            Vector3 preferredOffset = profile.followOffset;

            // 后方贴墙时缩短偏移，避免特写相机穿入障碍。
            Vector3 origin = followProxy.transform.position;
            Vector3 worldOffset = followProxy.transform.TransformDirection(preferredOffset);
            Vector3 worldTargetPos = origin + worldOffset;
            if (Physics.Linecast(origin, worldTargetPos, out RaycastHit hit, obstacleLayers))
            {
                float safeDist = Mathf.Max(0.8f, hit.distance - 0.2f);
                preferredOffset.z = -safeDist;
            }

            // 配置跟随与瞄准，保持处决动作在画面中心。
            CinemachineTransposer transposer = finisherVcam.GetCinemachineComponent<CinemachineTransposer>();
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            transposer.m_FollowOffset = preferredOffset;
            transposer.m_XDamping = 0f;
            transposer.m_YDamping = 0f;
            transposer.m_ZDamping = 0f;
            transposer.m_YawDamping = 0f;
            transposer.m_PitchDamping = 0f;

            CinemachineComposer composer = finisherVcam.GetCinemachineComponent<CinemachineComposer>();
            composer.m_TrackedObjectOffset = profile.lookAtOffset;
            composer.m_LookaheadTime = 0f;
            composer.m_HorizontalDamping = 0.05f;
            composer.m_VerticalDamping = 0.05f;
            composer.m_ScreenX = 0.5f;
            composer.m_ScreenY = 0.48f;
            composer.m_DeadZoneWidth = 0f;
            composer.m_DeadZoneHeight = 0f;
            composer.m_SoftZoneWidth = 0.5f;
            composer.m_SoftZoneHeight = 0.5f;

            finisherVcam.m_Lens.FieldOfView = profile.fov;
            finisherVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;

            // 提高优先级切入特写。
            finisherVcam.Priority = finisherPriority;

            // 按配置启动缓慢推近。
            if (profile.enableDolly)
            {
                if (finisherDollyCo != null) StopCoroutine(finisherDollyCo);
                finisherDollyCo = StartCoroutine(FinisherDollyRoutine(transposer, preferredOffset, profile.dollyDistance, profile.dollyDuration));
            }
        }

        private IEnumerator FinisherDollyRoutine(
            CinemachineTransposer transposer,
            Vector3 startOffset,
            float distance,
            float duration)
        {
            float elapsed = 0f;
            Vector3 targetOffset = startOffset + Vector3.forward * distance; // 向前（受害者方向）微量推近

            while (elapsed < duration && isInFinisher && transposer != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transposer.m_FollowOffset = Vector3.Lerp(startOffset, targetOffset, t);
                yield return null;
            }
            finisherDollyCo = null;
        }

        private void HandleFinisherEnded(CharacterBody player, CharacterBody victim)
        {
            isInFinisher = false;

            if (finisherDollyCo != null)
            {
                StopCoroutine(finisherDollyCo);
                finisherDollyCo = null;
            }

            finisherVcam.Priority = 0;

            // 恢复之前的相机状态
            bool isLocked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn;
            if (isLocked)
            {
                ApplyLock();
            }
            else
            {
                ApplyFree();
            }
        }

        // 在 Awake 配置机位和输入，再缓存表现偏移使用的基准值。
        private void InitializeCamera()
        {
            ConfigureFreeLookFollow();
            ConfigureOrbitInput();
            ApplyFreeLookAim();
            ApplyFreeLookBody();
            ConfigureLockVcam();
            ConfigureFinisherVcam();
            ConfigureBrainBlend();
            EnsureObstacleFader();
            CaptureCameraBases();
        }

        private void EnsureObstacleFader()
        {
            if (obstacleFader == null)
                obstacleFader = GetComponent<CameraObstacleFader>();
            if (obstacleFader == null)
                obstacleFader = gameObject.AddComponent<CameraObstacleFader>();
            obstacleFader.Configure(playerFollow);
        }

        // 缓存机位基准值：重箭镜头反应在基准上做偏移，结束精确归零
        private void CaptureCameraBases()
        {
            lockTransposer = lockVcam.GetCinemachineComponent<CinemachineTransposer>();
            lockComposer = lockVcam.GetCinemachineComponent<CinemachineComposer>();
            lockBaseOffset = followOffset;

            baseOrbitRadius = new float[3];
            baseOrbitHeight = new float[3];
            for (int i = 0; i < 3; i++)
            {
                baseOrbitRadius[i] = freeLook.m_Orbits[i].m_Radius;
                baseOrbitHeight[i] = freeLook.m_Orbits[i].m_Height;
            }
        }

        private void ConfigureFreeLookFollow()
        {
            freeLook.Follow = followProxy.transform;
            freeLook.LookAt = followProxy.transform;
        }

        // 自由镜头直接瞄准跟随点，避免瞄准阻尼叠加出额外延迟。
        private void ApplyFreeLookAim()
        {
            for (int i = 0; i < 3; i++)
            {
                CinemachineVirtualCamera rig = freeLook.GetRig(i);
                UseHardLookAt(rig);
            }
        }

        private static void UseHardLookAt(CinemachineVirtualCamera vcam)
        {
            if (vcam.GetCinemachineComponent<CinemachineHardLookAt>() != null) return;
            if (vcam.GetCinemachineComponent<CinemachineComposer>() != null)
                vcam.DestroyCinemachineComponent<CinemachineComposer>();
            vcam.AddCinemachineComponent<CinemachineHardLookAt>();
        }

        private void ApplyFreeLookBody()
        {
            freeLook.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            freeLook.m_Heading.m_Definition = CinemachineOrbitalTransposer.Heading.HeadingDefinition.PositionDelta;
            freeLook.m_Heading.m_VelocityFilterStrength = 0;
            freeLook.m_RecenterToTargetHeading.m_enabled = false;
            freeLook.m_YAxisRecentering.m_enabled = false;

            for (int i = 0; i < 3; i++)
            {
                CinemachineVirtualCamera rig = freeLook.GetRig(i);
                CinemachineOrbitalTransposer orbital = rig.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                orbital.m_XDamping = 0f;
                orbital.m_YDamping = 0f;
                orbital.m_ZDamping = 0f;
                orbital.m_YawDamping = 0f;
                orbital.m_PitchDamping = 0f;
            }
        }

        // 由自定义输入组件直接更新轴值，关闭 Cinemachine 默认输入和轴速度以避免重复驱动。
        private void ConfigureOrbitInput()
        {
            CinemachineInputProvider provider = freeLook.GetComponent<CinemachineInputProvider>();
            if (provider != null) provider.enabled = false;

            CursorController.RegisterOrbitInput(orbitInput);

            freeLook.m_XAxis.m_MaxSpeed = 0f;
            freeLook.m_YAxis.m_MaxSpeed = 0f;
        }

        // 将相机和混合统一到 LateUpdate，保持切镜与跟随的更新阶段一致。
        private void ConfigureBrainBlend()
        {
            brain = UnityEngine.Camera.main.GetComponent<CinemachineBrain>();

            brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
            brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
            brain.m_DefaultBlend = new CinemachineBlendDefinition(
                CinemachineBlendDefinition.Style.EaseInOut, Mathf.Max(0.05f, blendTime));

            ApplyTransitionHints(freeLook);
            ApplyTransitionHints(lockVcam);
            ApplyTransitionHints(finisherVcam);
        }

        private static void ApplyTransitionHints(CinemachineFreeLook vcam)
        {
            // 开局不要 Inherit 场景主相机：编辑器里主相机常在角色侧面。
            vcam.m_Transitions.m_InheritPosition = false;
            vcam.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        }

        private static void ApplyTransitionHints(CinemachineVirtualCamera vcam)
        {
            vcam.m_Transitions.m_InheritPosition = true;
            vcam.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        }

        private void ConfigureLockVcam()
        {
            lockVcam.Follow = followProxy.transform;
            lockVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            lockVcam.m_Lens.FieldOfView = freeLook.m_Lens.FieldOfView;

            ApplyLockBodySettings();
            ConfigureColliderOnVcam(lockVcam);
        }

        private void ConfigureFinisherVcam()
        {
            CinemachineTransposer transposer = finisherVcam.GetCinemachineComponent<CinemachineTransposer>();
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            transposer.m_FollowOffset = groundFinisherProfile.followOffset;
            transposer.m_XDamping = 0f;
            transposer.m_YDamping = 0f;
            transposer.m_ZDamping = 0f;
            transposer.m_YawDamping = 0f;
            transposer.m_PitchDamping = 0f;

            CinemachineComposer composer = finisherVcam.GetCinemachineComponent<CinemachineComposer>();
            composer.m_TrackedObjectOffset = groundFinisherProfile.lookAtOffset;
            composer.m_LookaheadTime = 0f;
            composer.m_HorizontalDamping = 0.05f;
            composer.m_VerticalDamping = 0.05f;
            composer.m_ScreenX = 0.5f;
            composer.m_ScreenY = 0.48f;
            composer.m_DeadZoneWidth = 0f;
            composer.m_DeadZoneHeight = 0f;
            composer.m_SoftZoneWidth = 0.5f;
            composer.m_SoftZoneHeight = 0.5f;

            ApplyTransitionHints(finisherVcam);
            ConfigureColliderOnVcam(finisherVcam);
        }

        private void ApplyLockBodySettings()
        {
            CinemachineTransposer transposer = lockVcam.GetCinemachineComponent<CinemachineTransposer>();
            transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
            transposer.m_FollowOffset = followOffset;
            transposer.m_XDamping = 0f;
            transposer.m_YDamping = 0f;
            transposer.m_ZDamping = 0f;
            transposer.m_YawDamping = 0f;
            transposer.m_PitchDamping = 0f;

            ApplyLockAimOffset();
            ApplyTransitionHints(lockVcam);
        }

        private void ApplyLockAimOffset()
        {
            CinemachineComposer composer = lockVcam.GetCinemachineComponent<CinemachineComposer>();

            composer.m_TrackedObjectOffset = lookAtOffset;
            composer.m_LookaheadTime = 0f;
            composer.m_HorizontalDamping = 0f;
            composer.m_VerticalDamping = 0f;
            composer.m_ScreenX = 0.5f;
            composer.m_ScreenY = 0.42f;
            composer.m_DeadZoneWidth = 0f;
            composer.m_DeadZoneHeight = 0f;
            composer.m_SoftZoneWidth = 0.8f;
            composer.m_SoftZoneHeight = 0.8f;
        }

        private void ConfigureColliderOnVcam(CinemachineVirtualCamera vcam)
        {
            CinemachineCollider vcamCollider = vcam.GetComponent<CinemachineCollider>();
            vcamCollider.m_AvoidObstacles = true;
            vcamCollider.m_CollideAgainst = obstacleLayers;
            vcamCollider.m_TransparentLayers = (1 << 5) | (1 << 6) | (1 << 7); // UI / Hurtbox / LockOnMarker
            vcamCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
            vcamCollider.m_CameraRadius = 0.25f;
            vcamCollider.m_MinimumDistanceFromTarget = 0.4f;
            // 平滑障碍挤压和回位；距离过近时由玩家淡出保持视野。
            vcamCollider.m_Damping = 0.3f;
            vcamCollider.m_DampingWhenOccluded = 0.15f;
            vcamCollider.m_SmoothingTime = 0.2f;
        }
    }

}
