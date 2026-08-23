using System.Collections;
using UnityEngine;
using Cinemachine;

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

    [Tooltip("识破踩刀处决机位（Finsher_Mikiri）")]
    [SerializeField] private FinisherCameraProfile mikiriFinisherProfile = new FinisherCameraProfile
    {
        followOffset = new Vector3(-0.42f, -0.15f, -2.7f),
        lookAtOffset = new Vector3(0f, 1.05f, 0f),
        fov = 47f,
        enableDolly = true,
        dollyDistance = 0.38f,
        dollyDuration = 1.5f
    };

    [Header("避障配置")]
    [Tooltip("会挡住镜头的层。玩家/Boss 在 Hurtbox，不要勾进去，否则会把镜头拉进角色")]
    [SerializeField] private LayerMask obstacleLayers = 1 | (1 << 3); // Default + Ground

    private bool setupDone;
    private CinemachineBrain brain;
    private Coroutine releaseLockYawCo;
    private Coroutine finisherDollyCo;
    private bool isInFinisher;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        CombatEventBus.OnLockOnChanged += HandleLockOnChanged;
        CombatEventBus.OnFinisherStarted += HandleFinisherStarted;
        CombatEventBus.OnFinisherEnded += HandleFinisherEnded;
    }

    private void OnDisable()
    {
        CombatEventBus.OnLockOnChanged -= HandleLockOnChanged;
        CombatEventBus.OnFinisherStarted -= HandleFinisherStarted;
        CombatEventBus.OnFinisherEnded -= HandleFinisherEnded;

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
        // 进 Play 时按当前锁定状态对齐一次（非轮询，只做初始化）
        bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn;
        HandleLockOnChanged(locked);
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

    // World Space 下 X=0 在目标世界 -Z；X=角色 yaw 时镜头在 -forward，即背后。
    private void SnapFreeLookBehindPlayer()
    {
        if (freeLook == null) return;

        Transform player = playerFollow != null ? playerFollow
            : (followProxy != null ? followProxy.source : null);
        if (player != null)
        {
            Vector3 fwd = player.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.001f)
            {
                fwd.Normalize();
                float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
                if (yaw > 180f) yaw -= 360f;
                else if (yaw < -180f) yaw += 360f;
                freeLook.m_XAxis.Value = yaw;
            }
        }

        float y = orbitInput != null ? orbitInput.YCenter : 0.45f;
        freeLook.m_YAxis.Value = Mathf.Clamp(y, 0.02f, 0.98f);

        Camera live = GetComponent<Camera>();
        if (live == null) live = Camera.main;
        if (live != null)
            freeLook.ForceCameraPosition(live.transform.position, live.transform.rotation);
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
        setupDone = freeLook != null && lockVcam != null && finisherVcam != null;
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
        vcam.m_Transitions.m_InheritPosition = true;
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
            GameObject existing = GameObject.Find("LockOn Camera");
            if (existing != null)
                lockVcam = existing.GetComponent<CinemachineVirtualCamera>();
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
        vcamCollider.m_Damping = 0.15f;
        vcamCollider.m_DampingWhenOccluded = 0.05f;
        vcamCollider.m_SmoothingTime = 0.08f;
    }
}
