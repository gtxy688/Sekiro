using System.Collections;
using UnityEngine;
using Cinemachine;

// M12 锁定相机：订阅 OnLockOnChanged，用两台 VCam 的 Priority 让 Brain 混合切换。
// 不每帧轮询 IsLockedOn。锁定时切第三人称跟随（跟着角色 yaw、看向 Boss），
// 解锁时把 FreeLook 钉在角色背后，不混回锁定前的环绕角。
public class CameraController : MonoBehaviour
{
    [Header("虚拟相机")]
    [SerializeField] private CinemachineFreeLook freeLook;
    [SerializeField] private CinemachineVirtualCamera lockVcam;

    [Header("跟随")]
    [Tooltip("玩家根，只用来找人和给跟随点 source。相机不要直接 Follow 这里。")]
    [SerializeField] private Transform playerFollow;

    [SerializeField] private CameraFollowTarget followProxy;
    [SerializeField] private CinemachineOrbitInput orbitInput;

    [Header("切镜")]
    [Tooltip("FreeLook ↔ 锁定的混合时长。太短像硬切，太长拖沓")]
    [SerializeField] private float blendTime = 0.6f;
    [SerializeField] private int freePriority = 10;
    [SerializeField] private int lockPriority = 20;

    [Header("锁定机位（相对平滑跟随点，点已在胸口高度）")]
    [Tooltip("略偏肩后。Y 不要再加身高，跟随点已经抬到胸口")]
    [SerializeField] private Vector3 followOffset = new Vector3(0.35f, 0.2f, -3.4f);
    [Tooltip("LookAt 相对 Boss 根的胸口偏移")]
    [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1.2f, 0f);

    [Header("锁定避障")]
    [Tooltip("会挡住镜头的层。玩家/Boss 在 Hurtbox，不要勾进去，否则会把镜头拉进角色")]
    [SerializeField] private LayerMask obstacleLayers = 1 | (1 << 3); // Default + Ground

    private bool setupDone;
    private CinemachineBrain brain;
    private Coroutine releaseLockYawCo;

    private void Awake()
    {
        EnsureSetup();
    }

    private void OnEnable()
    {
        CombatEventBus.OnLockOnChanged += HandleLockOnChanged;
    }

    private void OnDisable()
    {
        CombatEventBus.OnLockOnChanged -= HandleLockOnChanged;
        if (releaseLockYawCo != null)
        {
            StopCoroutine(releaseLockYawCo);
            releaseLockYawCo = null;
        }
    }

    private void Start()
    {
        EnsureSetup();
        // 进 Play 时按当前锁定状态对齐一次（不是轮询，只初始化）
        bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn;
        HandleLockOnChanged(locked);
    }

    private void HandleLockOnChanged(bool isLocked)
    {
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
        if (orbitInput != null) orbitInput.enabled = false;
    }

    private void ApplyFree()
    {
        // 混合期间锁定相机仍算 live，会每帧重算机位。
        // 这时若清掉跟随点 yaw，混合起点会甩到世界 -Z（角色侧方），看起来就是绕回去。
        // 必须等混合结束、锁定相机不再参与混合，才能把 yaw 清掉。
        SnapFreeLookBehindPlayer();
        if (freeLook != null)
            freeLook.m_RecenterToTargetHeading.CancelRecentering();

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
    // 同时用当前机位 ForceCameraPosition，把三个 Rig 的轴也钉住，避免混回锁定前的环绕角。
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

    private void EnsureSetup()
    {
        if (setupDone && freeLook != null && lockVcam != null && playerFollow != null) return;

        if (playerFollow == null)
        {
            PlayerBrain brain = FindObjectOfType<PlayerBrain>();
            if (brain != null) playerFollow = brain.transform;
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
        EnsureBrainBlend();
        setupDone = freeLook != null && lockVcam != null;
    }

    // Follow 这个点而不是玩家根：位置硬跟，但不吃动画 yaw。
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

    // Composer 带 Damping 会追着纠偏，走路发糊。HardLookAt 死盯跟随点。
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

    // 无锁定发糊：FreeLook 默认 X Damping=1，Heading 还按位移滤波，镜头慢半拍。
    // 朝向只跟鼠标（World），位置阻尼清零，和锁定相机一样贴人。
    private void ApplyFreeLookBody()
    {
        if (freeLook == null) return;

        // 2.10 的 Heading 没有 World。朝向不跟人转靠 BindingMode.WorldSpace；
        // PositionDelta 关掉速度滤波，避免走路时镜头慢半拍发糊。
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
        // 柱面插值 + InheritPosition：从当前机位滑到目标机位，不走直线穿模
        brain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut, Mathf.Max(0.05f, blendTime));

        ApplyTransitionHints(freeLook);
        ApplyTransitionHints(lockVcam);
    }

    // 2.10 的 m_Transitions 在 FreeLook / VirtualCamera 上，不在基类
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
        EnsureLockCollider();
    }

    private void ApplyLockBodySettings()
    {
        if (lockVcam == null) return;

        CinemachineTransposer transposer = lockVcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer != null)
        {
            // 绑玩家本地空间：角色面朝 Boss 时镜头在背后，围着目标走位时镜头跟着转
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

    // 锁定相机从 Follow 拉到 LookAt 的路径上撞墙就往前收，避免穿进场景。
    // 只打 Default/Ground：Hurtbox 是人，勾进去会把镜头吸进角色。
    private void EnsureLockCollider()
    {
        if (lockVcam == null) return;

        CinemachineCollider lockCollider = lockVcam.GetComponent<CinemachineCollider>();
        if (lockCollider == null)
            lockCollider = lockVcam.gameObject.AddComponent<CinemachineCollider>();

        lockCollider.m_AvoidObstacles = true;
        lockCollider.m_CollideAgainst = obstacleLayers;
        lockCollider.m_TransparentLayers = (1 << 5) | (1 << 6) | (1 << 7); // UI / Hurtbox / LockOnMarker
        lockCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
        lockCollider.m_CameraRadius = 0.25f;
        lockCollider.m_MinimumDistanceFromTarget = 0.4f;
        lockCollider.m_Damping = 0.15f;
        lockCollider.m_DampingWhenOccluded = 0.05f;
        lockCollider.m_SmoothingTime = 0.08f;
    }
}
