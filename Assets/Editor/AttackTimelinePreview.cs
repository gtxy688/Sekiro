using UnityEditor;
using UnityEngine;

using ARPG.FrameWork;
namespace ARPG.Editor
{

    public class AttackTimelinePreview
    {
        PreviewRenderUtility utility;
        GameObject instance;
        Animator animator;
        GameObject boundPrefab;

        Vector3 lookAt;
        float radius = 3f;
        float yaw;
        float pitch = 12f;

        public Animator Animator { get { return animator; } }

        public void Ensure(GameObject prefab)
        {
            if (prefab == boundPrefab && instance != null)
                return;
            CleanupInstance();
            boundPrefab = prefab;
            if (prefab == null) return;

            if (utility == null)
            {
                utility = new PreviewRenderUtility();
                utility.cameraFieldOfView = 25f;
                utility.camera.nearClipPlane = 0.01f;
                utility.camera.farClipPlane = 50f;
                if (utility.lights.Length > 0)
                {
                    utility.lights[0].intensity = 1.4f;
                    utility.lights[0].transform.rotation = Quaternion.Euler(40f, 40f, 0f);
                }
                if (utility.lights.Length > 1)
                    utility.lights[1].intensity = 0.6f;
            }

            instance = Object.Instantiate(prefab);
            instance.hideFlags = HideFlags.HideAndDontSave;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            utility.AddSingleGO(instance);
            animator = instance.GetComponentInChildren<Animator>();
            FrameCamera();
        }

        public void Orbit(float deltaYaw, float deltaPitch)
        {
            yaw += deltaYaw;
            pitch = Mathf.Clamp(pitch + deltaPitch, -80f, 80f);
            ApplyCamera();
        }

        public void Zoom(float scroll)
        {
            float scale = scroll > 0f ? 0.9f : 1.1f;
            radius = Mathf.Clamp(radius * scale, 0.35f, 18f);
            ApplyCamera();
        }

        public void ResetView()
        {
            FrameCamera();
        }

        public void Sample(string stateName, float normalizedTime)
        {
            if (animator == null || string.IsNullOrEmpty(stateName))
                return;
            if (!AnimUtil.HasState(animator, stateName))
                return;
            animator.Play(stateName, 0, Mathf.Clamp01(normalizedTime));
            animator.Update(0f);
        }

        public void Draw(Rect rect)
        {
            if (utility == null || instance == null)
            {
                EditorGUI.HelpBox(rect, "未指定预览 Prefab，或 Prefab 上没有可预览的物体。", MessageType.Info);
                return;
            }

            utility.BeginPreview(rect, GUIStyle.none);
            utility.camera.Render();
            Texture tex = utility.EndPreview();
            GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
        }

        public void Dispose()
        {
            CleanupInstance();
            if (utility != null)
            {
                utility.Cleanup();
                utility = null;
            }
            boundPrefab = null;
        }

        void CleanupInstance()
        {
            animator = null;
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
                instance = null;
            }
        }

        void FrameCamera()
        {
            if (utility == null || instance == null) return;
            Bounds b = new Bounds(instance.transform.position, Vector3.one);
            Renderer[] rs = instance.GetComponentsInChildren<Renderer>();
            bool has = false;
            for (int i = 0; i < rs.Length; i++)
            {
                if (!has)
                {
                    b = rs[i].bounds;
                    has = true;
                }
                else b.Encapsulate(rs[i].bounds);
            }
            lookAt = b.center;
            float size = Mathf.Max(b.extents.magnitude, 0.5f);
            radius = size * 2.4f;
            yaw = 20f;
            pitch = 12f;
            ApplyCamera();
        }

        void ApplyCamera()
        {
            if (utility == null) return;
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            utility.camera.transform.position = lookAt + rot * (Vector3.back * radius);
            utility.camera.transform.LookAt(lookAt);
        }
    }

}
