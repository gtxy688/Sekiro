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

    // M12 战斗相机系统：管理 FreeLook（自由探索）、LockOn（锁定跟随）与 Finisher（忍杀特写）三级虚拟相机。
    // 依靠 CombatEventBus 事件驱动，不每帧轮询状态。
    // 优先级层级：FreeLook (10) < LockOn (20) < Finisher (30)。
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

        // 参考 Docs/references/pics/识破忍杀：越肩后方机位，往右侧偏一点、略抬高俯视，
        // 瞄准点压低对准"玩家踩住 Boss 兵器"的关键画面，Boss 正面和踏刀点都要在画面里。
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

        private bool setupDone;
        // 陷阱3-3：可 Inspector 拖入主相机 Brain，避免懒加载 FindObjectOfType；
        // 未拖时仍走查找兜底（场景零改动）。
        [SerializeField] private CinemachineBrain brain;
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
            EnsureSetup();
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
            EnsureSetup();
            bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn;
            HandleLockOnChanged(locked);
            if (!locked)
                StartCoroutine(SnapBehindAfterFollowReady());
        }

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

        // JumpThrust：混合权重。下落还原主要跟髋高，这段只收尾。
        private void UpdateJumpThrustCamera()
        {
            if (isInFinisher) return;
            float speed = jumpThrustTarget > jumpThrustEnv ? jumpThrustBlendIn : jumpThrustBlendOut;
            if (speed <= 0.01f) speed = 0.25f;
            jumpThrustEnv = Mathf.MoveTowards(jumpThrustEnv, jumpThrustTarget, Time.deltaTime / speed);
            if (jumpThrustTarget <= 0f && jumpThrustEnv <= 0.001f)
            {
                jumpThrustEnv = 0f;
                jumpThrustBoss = null;
                jumpThrustPeakHipDy = 0f;
            }
        }

        // 镜头被墙挤死（实际距离远小于期望距离）→ 虚化玩家给镜头让位。
        // 用迟滞区间（enter 低 / exit 高）做开关，临界距离来回横跳时不会闪烁。
        private void UpdateObstacleFade()
        {
            if (obstacleFader == null) return;

            bool squeezed = false;
            if (!isInFinisher && followProxy != null && freeLook != null && lockVcam != null)
            {
                UnityEngine.Camera cam = UnityEngine.Camera.main;
                if (cam != null)
                {
                    float desired = GetDesiredFollowDistance();
                    float actual = Vector3.Distance(followProxy.transform.position, cam.transform.position);
                    float ratio = desired > 0.05f ? actual / desired : 1f;
                    squeezed = obstacleSqueezed
                        ? ratio < squeezeFadeExit   // 已虚化：回到安全比例才恢复
                        : ratio < squeezeFadeEnter; // 未虚化：压得更狠才触发
                }
            }

            obstacleSqueezed = squeezed;
            obstacleFader.SetOccluded(squeezed);
        }

        // 当前机位的"期望"镜头距离。永远用基准值算，避免和重箭反应偏移互相喂形成反馈回路。
        private float GetDesiredFollowDistance()
        {
            if (lockVcam.Priority > freeLook.Priority)
                return followOffset.magnitude;

            if (freeLook.m_Orbits == null || freeLook.m_Orbits.Length < 3 || baseOrbitRadius == null)
                return 3.2f;

            float y = Mathf.Clamp01(freeLook.m_YAxis.Value);
            // Cinemachine FreeLook：m_YAxis 0 = 底轨 m_Orbits[2]，0.5 = 中轨，1 = 顶轨 m_Orbits[0]
            return y <= 0.5f
                ? Mathf.Lerp(baseOrbitRadius[2], baseOrbitRadius[1], y * 2f)
                : Mathf.Lerp(baseOrbitRadius[1], baseOrbitRadius[0], (y - 0.5f) * 2f);
        }

        // 重箭命中：玩家借力后滑，镜头跟着下压 + 后拉，让"顶开"的力量感落在镜头上。
        private void HandleHeavyArrowDefended(CharacterBody body, bool perfect)
        {
            if (isInFinisher) return;
            if (body == null || playerFollow == null || body.transform != playerFollow) return;

            arrowBackPeak = perfect ? arrowDeflectBack : arrowGuardBack;
            arrowDownPeak = perfect ? arrowDeflectDown : arrowGuardDown;
            arrowReactStart = Time.time;
        }

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
            if (lockTransposer != null)
                lockTransposer.m_FollowOffset = lockBaseOffset
                    + new Vector3(0f, -jDrop - arrowDownPeak * e, -jBack - arrowBackPeak * e);

            if (lockComposer != null)
                lockComposer.m_TrackedObjectOffset = lookAtOffset + new Vector3(0f, lookUp * jumpThrustEnv, 0f);

            // 自由机位：推三条轨道的半径和高度
            if (freeLook != null && freeLook.m_Orbits != null && freeLook.m_Orbits.Length >= 3
                && baseOrbitRadius != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    freeLook.m_Orbits[i].m_Radius = baseOrbitRadius[i] + jBack + arrowBackPeak * e;
                    freeLook.m_Orbits[i].m_Height = baseOrbitHeight[i] - jDrop - arrowDownPeak * e;
                }
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

            EnsureSetup();
            if (freeLook == null || lockVcam == null) return;

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

            if (followProxy != null)
            {
                followProxy.SetYawTarget(target);
                lockVcam.Follow = followProxy.transform;
            }
            else
            {
                lockVcam.Follow = playerFollow;
            }
            lockVcam.LookAt = target;

            lockVcam.Priority = lockPriority;
            freeLook.Priority = 0;
            if (finisherVcam != null) finisherVcam.Priority = 0;
            if (orbitInput != null) orbitInput.enabled = false;
        }

        private void ApplyFree()
        {
            SnapFreeLookBehindPlayer();
            if (freeLook != null)
                freeLook.m_RecenterToTargetHeading.CancelRecentering();

            if (finisherVcam != null) finisherVcam.Priority = 0;
            if (lockVcam != null) lockVcam.Priority = 0;
            if (freeLook != null) freeLook.Priority = freePriority;
            if (orbitInput != null) orbitInput.enabled = true;

            if (releaseLockYawCo != null) StopCoroutine(releaseLockYawCo);
            releaseLockYawCo = StartCoroutine(ReleaseLockYawAfterBlend());
        }

        private IEnumerator ReleaseLockYawAfterBlend()
        {
            if (brain == null)
                brain = FindObjectOfType<CinemachineBrain>();

            yield return null;
            float timeout = Mathf.Max(0.05f, blendTime) + 0.25f;
            float elapsed = 0f;
            while (brain != null && brain.IsBlending && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (followProxy != null)
                followProxy.SetYawTarget(null);
            releaseLockYawCo = null;
        }

        // World Space 下 X=角色 yaw 时镜头在 -forward，即背后。
        // 不要 ForceCameraPosition(场景主相机)：编辑器里主相机经常在侧面，会把 FreeLook 钉在侧机位。
        private void SnapFreeLookBehindPlayer()
        {
            if (freeLook == null) return;

            Transform player = playerFollow != null ? playerFollow
                : (followProxy != null ? followProxy.source : null);
            if (player == null) return;

            Vector3 fwd = player.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.001f) return;
            fwd.Normalize();

            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            if (yaw > 180f) yaw -= 360f;
            else if (yaw < -180f) yaw += 360f;
            freeLook.m_XAxis.Value = yaw;

            float y = orbitInput != null ? orbitInput.YCenter : 0.45f;
            freeLook.m_YAxis.Value = Mathf.Clamp(y, 0.02f, 0.98f);

            float radius = 3.2f;
            if (freeLook.m_Orbits != null && freeLook.m_Orbits.Length > 1)
                radius = Mathf.Max(1.6f, freeLook.m_Orbits[1].m_Radius);

            float height = followProxy != null ? followProxy.height : 1.4f;
            Vector3 pivot = player.position + Vector3.up * height;
            Vector3 camPos = pivot - fwd * radius;
            freeLook.ForceCameraPosition(camPos, Quaternion.LookRotation(fwd, Vector3.up));

            if (orbitInput != null)
                orbitInput.IgnoreLookUntil(Time.unscaledTime + 0.45f);
        }

        // ===== 2. 处决特写运镜逻辑（只狼式刀刃侧低机位特写） =====

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
            EnsureSetup();
            if (finisherVcam == null || victim == null) return;

            isInFinisher = true;
            if (orbitInput != null) orbitInput.enabled = false;

            Transform playerT = player != null ? player.transform : playerFollow;
            Transform victimT = victim.transform;

            FinisherCameraProfile profile = GetProfileForKind(kind);

            // 1. 设置 Follow 与 LookAt：跟随主角跟随代理点，看向受害者
            if (followProxy != null)
            {
                followProxy.SetYawTarget(victimT);
                finisherVcam.Follow = followProxy.transform;
            }
            else
            {
                finisherVcam.Follow = playerT;
            }
            finisherVcam.LookAt = victimT;

            // 2. 处决机位：偏向刀刃侧、低机位微仰
            Vector3 preferredOffset = profile.followOffset;

            // 3. 避障检测：检查后方是否贴墙
            Vector3 origin = (followProxy != null ? followProxy.transform.position : (playerT != null ? playerT.position : transform.position));
            Vector3 worldOffset = (followProxy != null ? followProxy.transform.TransformDirection(preferredOffset) : (playerT != null ? playerT.TransformDirection(preferredOffset) : preferredOffset));
            Vector3 worldTargetPos = origin + worldOffset;
            if (Physics.Linecast(origin, worldTargetPos, out RaycastHit hit, obstacleLayers))
            {
                float safeDist = Mathf.Max(0.8f, hit.distance - 0.2f);
                preferredOffset.z = -safeDist;
            }

            // 4. 配置虚拟相机参数
            CinemachineTransposer transposer = finisherVcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
                transposer.m_FollowOffset = preferredOffset;
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
                transposer.m_YawDamping = 0f;
                transposer.m_PitchDamping = 0f;
            }

            CinemachineComposer composer = finisherVcam.GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
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
            }

            finisherVcam.m_Lens.FieldOfView = profile.fov;
            finisherVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;

            // 5. 提升优先级切入特写
            finisherVcam.Priority = finisherPriority;

            // 6. 处决微推镜头（Dolly In 沿攻击朝向向前推进）
            if (profile.enableDolly && transposer != null)
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

            if (finisherVcam != null)
            {
                finisherVcam.Priority = 0;
            }

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

        // ===== 3. 初始化与自动装配 =====

        public void EnsureSetup()
        {
            if (setupDone && freeLook != null && lockVcam != null && finisherVcam != null && playerFollow != null) return;

            if (playerFollow == null)
            {
                PlayerBrain brainComp = FindObjectOfType<PlayerBrain>();
                if (brainComp != null) playerFollow = brainComp.transform;
            }

            if (freeLook == null)
                freeLook = FindObjectOfType<CinemachineFreeLook>();

            if (freeLook != null)
            {
                EnsureFreeLookFollowProxy();
                EnsureOrbitInput();
                ApplyFreeLookAim();
                ApplyFreeLookBody();
            }

            EnsureLockVcam();
            EnsureFinisherVcam();
            EnsureBrainBlend();
            EnsureObstacleFader();
            CaptureCameraBases();
            setupDone = freeLook != null && lockVcam != null && finisherVcam != null;
        }

        private void EnsureObstacleFader()
        {
            if (obstacleFader == null)
                obstacleFader = GetComponent<CameraObstacleFader>();
            if (obstacleFader == null)
                obstacleFader = gameObject.AddComponent<CameraObstacleFader>();
            if (playerFollow != null)
                obstacleFader.Configure(playerFollow);
        }

        // 缓存机位基准值：重箭镜头反应在基准上做偏移，结束精确归零
        private void CaptureCameraBases()
        {
            lockTransposer = lockVcam != null
                ? lockVcam.GetCinemachineComponent<CinemachineTransposer>() : null;
            lockComposer = lockVcam != null
                ? lockVcam.GetCinemachineComponent<CinemachineComposer>() : null;
            lockBaseOffset = followOffset;

            if (freeLook == null || freeLook.m_Orbits == null || freeLook.m_Orbits.Length < 3) return;
            baseOrbitRadius = new float[3];
            baseOrbitHeight = new float[3];
            for (int i = 0; i < 3; i++)
            {
                baseOrbitRadius[i] = freeLook.m_Orbits[i].m_Radius;
                baseOrbitHeight[i] = freeLook.m_Orbits[i].m_Height;
            }
        }

        private void EnsureFreeLookFollowProxy()
        {
            if (followProxy == null)
                followProxy = FindObjectOfType<CameraFollowTarget>();
            if (followProxy == null)
            {
                GameObject go = new GameObject("CameraFollowTarget");
                followProxy = go.AddComponent<CameraFollowTarget>();
                if (playerFollow != null)
                {
                    followProxy.source = playerFollow;
                    go.transform.position = playerFollow.position + Vector3.up * followProxy.height;
                }
            }
            else if (followProxy.source == null && playerFollow != null)
            {
                followProxy.source = playerFollow;
            }

            if (freeLook.Follow != followProxy.transform)
                freeLook.Follow = followProxy.transform;
            if (freeLook.LookAt != followProxy.transform)
                freeLook.LookAt = followProxy.transform;
        }

        private void ApplyFreeLookAim()
        {
            if (freeLook == null) return;

            for (int i = 0; i < 3; i++)
            {
                CinemachineVirtualCamera rig = freeLook.GetRig(i);
                if (rig == null) continue;
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
            if (freeLook == null) return;

            freeLook.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
            freeLook.m_Heading.m_Definition = CinemachineOrbitalTransposer.Heading.HeadingDefinition.PositionDelta;
            freeLook.m_Heading.m_VelocityFilterStrength = 0;
            freeLook.m_RecenterToTargetHeading.m_enabled = false;
            freeLook.m_YAxisRecentering.m_enabled = false;

            for (int i = 0; i < 3; i++)
            {
                CinemachineVirtualCamera rig = freeLook.GetRig(i);
                if (rig == null) continue;
                CinemachineOrbitalTransposer orbital = rig.GetCinemachineComponent<CinemachineOrbitalTransposer>();
                if (orbital == null) continue;
                orbital.m_XDamping = 0f;
                orbital.m_YDamping = 0f;
                orbital.m_ZDamping = 0f;
                orbital.m_YawDamping = 0f;
                orbital.m_PitchDamping = 0f;
            }
        }

        private void EnsureOrbitInput()
        {
            if (freeLook == null) return;

            CinemachineInputProvider provider = freeLook.GetComponent<CinemachineInputProvider>();
            if (provider != null) provider.enabled = false;

            if (orbitInput == null)
                orbitInput = freeLook.GetComponent<CinemachineOrbitInput>();
            if (orbitInput == null)
                orbitInput = freeLook.gameObject.AddComponent<CinemachineOrbitInput>();

            CursorController.RegisterOrbitInput(orbitInput);

            freeLook.m_XAxis.m_MaxSpeed = 0f;
            freeLook.m_YAxis.m_MaxSpeed = 0f;
        }

        private void EnsureBrainBlend()
        {
            brain = FindObjectOfType<CinemachineBrain>();
            if (brain == null) return;

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
            if (vcam == null) return;
            // 开局不要 Inherit 场景主相机：编辑器里主相机常在角色侧面。
            vcam.m_Transitions.m_InheritPosition = false;
            vcam.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        }

        private static void ApplyTransitionHints(CinemachineVirtualCamera vcam)
        {
            if (vcam == null) return;
            vcam.m_Transitions.m_InheritPosition = true;
            vcam.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        }

        private void EnsureLockVcam()
        {
            if (lockVcam == null)
            {
                // 陷阱3-3 兜底：字符串查找改名即失效。理想路径是 Inspector 拖 lockVcam；
                // 走到这里说明没拖，告警一次提示接线（场景已存在同名物体时）。
                GameObject existing = GameObject.Find("LockOn Camera");
                if (existing != null)
                {
                    lockVcam = existing.GetComponent<CinemachineVirtualCamera>();
                    Debug.LogWarning("[CameraController] 锁定相机经字符串查找获得（\"LockOn Camera\"），请在 Inspector 拖入 lockVcam，避免改名后查找失效。");
                }
            }

            if (lockVcam == null)
            {
                GameObject go = new GameObject("LockOn Camera");
                lockVcam = go.AddComponent<CinemachineVirtualCamera>();
                lockVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
                lockVcam.m_Lens.FieldOfView = freeLook != null ? freeLook.m_Lens.FieldOfView : 60f;
                lockVcam.Priority = 0;
            }

            if (lockVcam.GetCinemachineComponent<CinemachineTransposer>() == null)
                lockVcam.AddCinemachineComponent<CinemachineTransposer>();
            if (lockVcam.GetCinemachineComponent<CinemachineComposer>() == null)
                lockVcam.AddCinemachineComponent<CinemachineComposer>();

            if (followProxy != null)
                lockVcam.Follow = followProxy.transform;
            else if (playerFollow != null)
                lockVcam.Follow = playerFollow;

            lockVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
            if (freeLook != null)
                lockVcam.m_Lens.FieldOfView = freeLook.m_Lens.FieldOfView;

            ApplyLockBodySettings();
            EnsureColliderOnVcam(lockVcam);
        }

        private void EnsureFinisherVcam()
        {
            if (finisherVcam == null)
            {
                GameObject existing = GameObject.Find("Finisher Camera");
                if (existing != null)
                    finisherVcam = existing.GetComponent<CinemachineVirtualCamera>();
            }

            if (finisherVcam == null)
            {
                GameObject go = new GameObject("Finisher Camera");
                finisherVcam = go.AddComponent<CinemachineVirtualCamera>();
                finisherVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
                finisherVcam.m_Lens.FieldOfView = groundFinisherProfile.fov;
                finisherVcam.Priority = 0;
            }

            if (finisherVcam.GetCinemachineComponent<CinemachineTransposer>() == null)
                finisherVcam.AddCinemachineComponent<CinemachineTransposer>();
            if (finisherVcam.GetCinemachineComponent<CinemachineComposer>() == null)
                finisherVcam.AddCinemachineComponent<CinemachineComposer>();

            CinemachineTransposer transposer = finisherVcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
                transposer.m_FollowOffset = groundFinisherProfile.followOffset;
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
                transposer.m_YawDamping = 0f;
                transposer.m_PitchDamping = 0f;
            }

            CinemachineComposer composer = finisherVcam.GetCinemachineComponent<CinemachineComposer>();
            if (composer != null)
            {
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
            }

            ApplyTransitionHints(finisherVcam);
            EnsureColliderOnVcam(finisherVcam);
        }

        private void ApplyLockBodySettings()
        {
            if (lockVcam == null) return;

            CinemachineTransposer transposer = lockVcam.GetCinemachineComponent<CinemachineTransposer>();
            if (transposer != null)
            {
                transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
                transposer.m_FollowOffset = followOffset;
                transposer.m_XDamping = 0f;
                transposer.m_YDamping = 0f;
                transposer.m_ZDamping = 0f;
                transposer.m_YawDamping = 0f;
                transposer.m_PitchDamping = 0f;
            }

            ApplyLockAimOffset();
            ApplyTransitionHints(lockVcam);
        }

        private void ApplyLockAimOffset()
        {
            if (lockVcam == null) return;
            CinemachineComposer composer = lockVcam.GetCinemachineComponent<CinemachineComposer>();
            if (composer == null) return;

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

        private void EnsureColliderOnVcam(CinemachineVirtualCamera vcam)
        {
            if (vcam == null) return;

            CinemachineCollider vcamCollider = vcam.GetComponent<CinemachineCollider>();
            if (vcamCollider == null)
                vcamCollider = vcam.gameObject.AddComponent<CinemachineCollider>();

            vcamCollider.m_AvoidObstacles = true;
            vcamCollider.m_CollideAgainst = obstacleLayers;
            vcamCollider.m_TransparentLayers = (1 << 5) | (1 << 6) | (1 << 7); // UI / Hurtbox / LockOnMarker
            vcamCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
            vcamCollider.m_CameraRadius = 0.25f;
            vcamCollider.m_MinimumDistanceFromTarget = 0.4f;
            // 阻尼/平滑都调缓：被墙挤近和松开回位都慢慢来，
            // 靠帧内硬位移是绕墙抖动的主要来源；挤死时的观感交给玩家虚化兜底
            vcamCollider.m_Damping = 0.3f;
            vcamCollider.m_DampingWhenOccluded = 0.15f;
            vcamCollider.m_SmoothingTime = 0.2f;
        }
    }

}
