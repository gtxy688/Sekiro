using Cinemachine;
using UnityEditor;
using UnityEngine;

// 把锁定用的第三人称 VCam + CameraController 写进当前场景，方便在 Inspector 调机位。
// 不跑菜单也能 Play：CameraController 运行时会自己补 LockOn Camera。
// 用法：打开 GameScene → Tools/战斗/生成锁定相机
public static class LockOnCameraBuilder
{
    [MenuItem("Tools/战斗/生成锁定相机")]
    public static void Build()
    {
        CinemachineFreeLook freeLook = Object.FindObjectOfType<CinemachineFreeLook>();
        PlayerBrain player = Object.FindObjectOfType<PlayerBrain>();
        CinemachineBrain brain = Object.FindObjectOfType<CinemachineBrain>();
        if (freeLook == null || player == null || brain == null)
        {
            EditorUtility.DisplayDialog("生成锁定相机",
                "场景里需要 FreeLook Camera、玩家（PlayerBrain）和挂了 CinemachineBrain 的主相机。请先打开 GameScene。",
                "确定");
            return;
        }

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

        CinemachineTransposer transposer = lockVcam.GetCinemachineComponent<CinemachineTransposer>();
        if (transposer == null)
            transposer = lockVcam.AddCinemachineComponent<CinemachineTransposer>();
        transposer.m_BindingMode = CinemachineTransposer.BindingMode.LockToTargetWithWorldUp;
        transposer.m_FollowOffset = new Vector3(0.35f, 0.2f, -3.4f);
        transposer.m_XDamping = 0f;
        transposer.m_YDamping = 0f;
        transposer.m_ZDamping = 0f;
        transposer.m_YawDamping = 0f;

        CinemachineComposer composer = lockVcam.GetCinemachineComponent<CinemachineComposer>();
        if (composer == null)
            composer = lockVcam.AddCinemachineComponent<CinemachineComposer>();
        composer.m_TrackedObjectOffset = new Vector3(0f, 1.2f, 0f);
        composer.m_LookaheadTime = 0f;
        composer.m_HorizontalDamping = 0f;
        composer.m_VerticalDamping = 0f;
        composer.m_ScreenY = 0.42f;
        composer.m_DeadZoneWidth = 0f;
        composer.m_DeadZoneHeight = 0f;
        composer.m_SoftZoneWidth = 0.8f;
        composer.m_SoftZoneHeight = 0.8f;
        EditorUtility.SetDirty(lockVcam);

        Undo.RecordObject(brain, "Brain LateUpdate blend");
        brain.m_UpdateMethod = CinemachineBrain.UpdateMethod.LateUpdate;
        brain.m_BlendUpdateMethod = CinemachineBrain.BrainUpdateMethod.LateUpdate;
        brain.m_DefaultBlend = new CinemachineBlendDefinition(
            CinemachineBlendDefinition.Style.EaseInOut, 0.6f);
        EditorUtility.SetDirty(brain);

        CameraController controller = Object.FindObjectOfType<CameraController>();
        if (controller == null)
            controller = Undo.AddComponent<CameraController>(brain.gameObject);

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("freeLook").objectReferenceValue = freeLook;
        so.FindProperty("lockVcam").objectReferenceValue = lockVcam;
        so.FindProperty("playerFollow").objectReferenceValue = player.transform;
        so.FindProperty("followProxy").objectReferenceValue = proxy;
        so.FindProperty("orbitInput").objectReferenceValue = orbit;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(controller);

        Selection.activeGameObject = lockGo;
        Debug.Log("[LockOnCamera] 已生成 LockOn Camera，并接到 Main Camera 的 CameraController。Ctrl+S 存场景。");
    }
}
