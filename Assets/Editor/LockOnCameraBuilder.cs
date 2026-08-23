using Cinemachine;
using UnityEditor;
using UnityEngine;

// 把战斗用相机系统（FreeLook + LockOn Camera + Finisher Camera）及 CameraController 写入当前场景。
// 不跑菜单也能 Play：CameraController 运行时也会动态自检和自补虚拟相机。
// 用法：打开 GameScene -> 菜单 Tools/战斗/生成战斗相机
public static class LockOnCameraBuilder
{
    [MenuItem("Tools/战斗/生成战斗相机")]
    [MenuItem("Tools/战斗/生成锁定相机")]
    public static void Build()
    {
        CinemachineFreeLook freeLook = Object.FindObjectOfType<CinemachineFreeLook>();
        PlayerBrain player = Object.FindObjectOfType<PlayerBrain>();
        CinemachineBrain brain = Object.FindObjectOfType<CinemachineBrain>();
        if (freeLook == null || player == null || brain == null)
        {
            EditorUtility.DisplayDialog("生成战斗相机",
                "场景里需要 FreeLook Camera、玩家（PlayerBrain）和挂了 CinemachineBrain 的主相机。请先打开 GameScene。",
                "确定");
            return;
        }

        // 1. 跟随代理点（CameraFollowTarget）
        CameraFollowTarget proxy = Object.FindObjectOfType<CameraFollowTarget>();
        if (proxy == null)
        {
            GameObject proxyGo = new GameObject("CameraFollowTarget");
            Undo.RegisterCreatedObjectUndo(proxyGo, "Create CameraFollowTarget");
            proxy = proxyGo.AddComponent<CameraFollowTarget>();
            proxy.source = player.transform;
            proxyGo.transform.position = player.transform.position + Vector3.up * proxy.height;
        }
        else if (proxy.source == null)
        {
            Undo.RecordObject(proxy, "Assign CameraFollowTarget source");
            proxy.source = player.transform;
        }

        // 2. 配置 FreeLook Camera
        Undo.RecordObject(freeLook, "Wire FreeLook follow proxy");
        freeLook.Follow = proxy.transform;
        freeLook.LookAt = proxy.transform;
        freeLook.m_BindingMode = CinemachineTransposer.BindingMode.WorldSpace;
        freeLook.m_Heading.m_Definition = CinemachineOrbitalTransposer.Heading.HeadingDefinition.PositionDelta;
        freeLook.m_Heading.m_VelocityFilterStrength = 0;
        freeLook.m_RecenterToTargetHeading.m_enabled = false;
        freeLook.m_YAxisRecentering.m_enabled = false;
        freeLook.m_Transitions.m_InheritPosition = true;
        freeLook.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        freeLook.m_XAxis.m_MaxSpeed = 0f;
        freeLook.m_YAxis.m_MaxSpeed = 0f;
        for (int i = 0; i < 3; i++)
        {
            CinemachineVirtualCamera rig = freeLook.GetRig(i);
            if (rig == null) continue;
            if (rig.GetCinemachineComponent<CinemachineHardLookAt>() == null)
            {
                if (rig.GetCinemachineComponent<CinemachineComposer>() != null)
                    rig.DestroyCinemachineComponent<CinemachineComposer>();
                rig.AddCinemachineComponent<CinemachineHardLookAt>();
            }
            CinemachineOrbitalTransposer orbital = rig.GetCinemachineComponent<CinemachineOrbitalTransposer>();
            if (orbital != null)
            {
                orbital.m_XDamping = 0f;
                orbital.m_YDamping = 0f;
                orbital.m_ZDamping = 0f;
            }
        }
        EditorUtility.SetDirty(freeLook);

        CinemachineInputProvider provider = freeLook.GetComponent<CinemachineInputProvider>();
        if (provider != null)
        {
            Undo.RecordObject(provider, "Disable FreeLook InputProvider");
            provider.enabled = false;
        }

        CinemachineOrbitInput orbit = freeLook.GetComponent<CinemachineOrbitInput>();
        if (orbit == null)
            orbit = Undo.AddComponent<CinemachineOrbitInput>(freeLook.gameObject);

        // 3. 配置 LockOn Camera
        GameObject lockGo = GameObject.Find("LockOn Camera");
        CinemachineVirtualCamera lockVcam = lockGo != null
            ? lockGo.GetComponent<CinemachineVirtualCamera>()
            : null;
        if (lockVcam == null)
        {
            lockGo = new GameObject("LockOn Camera");
            Undo.RegisterCreatedObjectUndo(lockGo, "Create LockOn Camera");
            lockVcam = lockGo.AddComponent<CinemachineVirtualCamera>();
        }

        Undo.RecordObject(lockVcam, "Configure LockOn Camera");
        lockVcam.Follow = proxy.transform;
        lockVcam.LookAt = null;
        lockVcam.Priority = 0;
        lockVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        lockVcam.m_Transitions.m_InheritPosition = true;
        lockVcam.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        lockVcam.m_Lens.FieldOfView = freeLook.m_Lens.FieldOfView;

        CinemachineTransposer lockTransposer = lockVcam.GetCinemachineComponent<CinemachineTransposer>();
        if (lockTransposer == null)
            lockTransposer = lockVcam.AddCinemachineComponent<CinemachineTransposer>();
        lockTransposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        lockTransposer.m_FollowOffset = new Vector3(0.35f, 0.2f, -3.4f);
        lockTransposer.m_XDamping = 0f;
        lockTransposer.m_YDamping = 0f;
        lockTransposer.m_ZDamping = 0f;
        lockTransposer.m_YawDamping = 0f;

        CinemachineComposer lockComposer = lockVcam.GetCinemachineComponent<CinemachineComposer>();
        if (lockComposer == null)
            lockComposer = lockVcam.AddCinemachineComponent<CinemachineComposer>();
        lockComposer.m_TrackedObjectOffset = new Vector3(0f, 1.2f, 0f);
        lockComposer.m_LookaheadTime = 0f;
        lockComposer.m_HorizontalDamping = 0f;
        lockComposer.m_VerticalDamping = 0f;
        lockComposer.m_ScreenY = 0.42f;
        lockComposer.m_DeadZoneWidth = 0f;
        lockComposer.m_DeadZoneHeight = 0f;
        lockComposer.m_SoftZoneWidth = 0.8f;
        lockComposer.m_SoftZoneHeight = 0.8f;
        EditorUtility.SetDirty(lockVcam);

        CinemachineCollider lockCollider = lockVcam.GetComponent<CinemachineCollider>();
        if (lockCollider == null)
            lockCollider = lockVcam.gameObject.AddComponent<CinemachineCollider>();
        lockCollider.m_AvoidObstacles = true;
        lockCollider.m_CollideAgainst = (1 << 0) | (1 << 3);
        lockCollider.m_TransparentLayers = (1 << 5) | (1 << 6) | (1 << 7);
        lockCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
        lockCollider.m_CameraRadius = 0.25f;
        EditorUtility.SetDirty(lockCollider);

        // 4. 配置 Finisher Camera (只狼式主角后方特写)
        GameObject finisherGo = GameObject.Find("Finisher Camera");
        CinemachineVirtualCamera finisherVcam = finisherGo != null
            ? finisherGo.GetComponent<CinemachineVirtualCamera>()
            : null;
        if (finisherVcam == null)
        {
            finisherGo = new GameObject("Finisher Camera");
            Undo.RegisterCreatedObjectUndo(finisherGo, "Create Finisher Camera");
            finisherVcam = finisherGo.AddComponent<CinemachineVirtualCamera>();
        }

        Undo.RecordObject(finisherVcam, "Configure Finisher Camera");
        finisherVcam.Priority = 0;
        finisherVcam.m_StandbyUpdate = CinemachineVirtualCameraBase.StandbyUpdateMode.Always;
        finisherVcam.m_Transitions.m_InheritPosition = true;
        finisherVcam.m_Transitions.m_BlendHint = CinemachineVirtualCameraBase.BlendHint.CylindricalPosition;
        finisherVcam.m_Lens.FieldOfView = 46f;

        CinemachineTransposer finisherTransposer = finisherVcam.GetCinemachineComponent<CinemachineTransposer>();
        if (finisherTransposer == null)
            finisherTransposer = finisherVcam.AddCinemachineComponent<CinemachineTransposer>();
        finisherTransposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        finisherTransposer.m_FollowOffset = new Vector3(-0.48f, -0.05f, -2.65f);
        finisherTransposer.m_XDamping = 0f;
        finisherTransposer.m_YDamping = 0f;
        finisherTransposer.m_ZDamping = 0f;

        CinemachineComposer finisherComposer = finisherVcam.GetCinemachineComponent<CinemachineComposer>();
        if (finisherComposer == null)
            finisherComposer = finisherVcam.AddCinemachineComponent<CinemachineComposer>();
        finisherComposer.m_TrackedObjectOffset = new Vector3(0f, 1.15f, 0f);
        finisherComposer.m_LookaheadTime = 0f;
        finisherComposer.m_HorizontalDamping = 0.05f;
        finisherComposer.m_VerticalDamping = 0.05f;
        finisherComposer.m_ScreenX = 0.5f;
        finisherComposer.m_ScreenY = 0.48f;
        EditorUtility.SetDirty(finisherVcam);

        CinemachineCollider finisherCollider = finisherVcam.GetComponent<CinemachineCollider>();
        if (finisherCollider == null)
            finisherCollider = finisherVcam.gameObject.AddComponent<CinemachineCollider>();
        finisherCollider.m_AvoidObstacles = true;
        finisherCollider.m_CollideAgainst = (1 << 0) | (1 << 3);
        finisherCollider.m_TransparentLayers = (1 << 5) | (1 << 6) | (1 << 7);
        finisherCollider.m_Strategy = CinemachineCollider.ResolutionStrategy.PreserveCameraHeight;
        finisherCollider.m_CameraRadius = 0.25f;
        EditorUtility.SetDirty(finisherCollider);

        // 5. 配置 CinemachineBrain
        Undo.RecordObject(brain, "Brain LateUpdate blend");
        brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
        brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut, 0.6f);
        EditorUtility.SetDirty(brain);

        // 6. 配置 CameraController 挂载与引用
        CameraController controller = Object.FindObjectOfType<CameraController>();
        if (controller == null)
            controller = Undo.AddComponent<CameraController>(brain.gameObject);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("freeLook").objectReferenceValue = freeLook;
        so.FindProperty("lockVcam").objectReferenceValue = lockVcam;
        so.FindProperty("finisherVcam").objectReferenceValue = finisherVcam;
        so.FindProperty("playerFollow").objectReferenceValue = player.transform;
        so.FindProperty("followProxy").objectReferenceValue = proxy;
        so.FindProperty("orbitInput").objectReferenceValue = orbit;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        Selection.activeGameObject = lockGo;
        Debug.Log("[CombatCamera] 已生成/配置 FreeLook、LockOn Camera 与 Finisher Camera（主角正后方特写机位），并自动绑定到 CameraController。请 Ctrl+S 保存场景。");
    }
}
