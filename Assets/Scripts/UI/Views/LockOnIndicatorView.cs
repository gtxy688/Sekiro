using UnityEngine;
using UnityEngine.Rendering.Universal;

using ARPG.Boss;
using ARPG.Combat;
using ARPG.FrameWork.Body;
namespace ARPG.UI
{

    // 世界空间锁定点：钉在 Spine1 上，Overlay 相机画在最前，不被模型挡住
    [ExecuteAlways]
    public class LockOnIndicatorView : UIView
    {
        private const string MarkerLayerName = "LockOnMarker";
        private const float WorldScale = 0.01f;

        [Header("跟踪")]
        [SerializeField] private Transform followTarget; // 拖 Spine1；空则自动找

        [Header("锁定 / 忍杀")]
        [SerializeField] private GameObject focusOn;
        [SerializeField] private GameObject finisher;

        private bool isFinisherReady;
        private bool isLocked;
        private UnityEngine.Camera overlayCam;
        private bool overlayOwned;
        private bool worldSetupDone;
        private int markerLayer = -1;

        private void OnDisable()
        {
            TearDownOverlayCamera();
            worldSetupDone = false;
        }

        public override void OnViewInit()
        {
            isFinisherReady = false;
            isLocked = false;
            ResolveRefs();
            if (Application.isPlaying)
            {
                EnsureWorldFollowSetup();
                AutoBindFollowTarget();
                Refresh();
            }
        }

        public void BindFollowTarget(CharacterBody boss)
        {
            if (boss == null) return;
            Transform found = FindDeep(boss.transform, "Spine1") ?? FindDeep(boss.transform, "Spine");
            if (found != null) followTarget = found;
        }

        public void SetLocked(bool locked)
        {
            isLocked = locked;
            Refresh();
        }

        public void SetFinisherReady(bool ready)
        {
            isFinisherReady = ready;
            Refresh();
        }

        private void LateUpdate()
        {
            ResolveRefs();
            if (Application.isPlaying)
                EnsureWorldFollowSetup();

            AutoBindFollowTarget();
            FollowWorld();
            if (Application.isPlaying)
                Refresh();
        }

    #if UNITY_EDITOR
        private void OnEnable()
        {
            ResolveRefs();
            if (!Application.isPlaying)
            {
                if (focusOn != null) focusOn.SetActive(true);
                if (finisher != null) finisher.SetActive(true);
            }
        }
    #endif

        private void FollowWorld()
        {
            if (followTarget == null) return;

            transform.position = followTarget.position;

            UnityEngine.Camera cam = overlayCam != null ? overlayCam : UnityEngine.Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }

        private void EnsureWorldFollowSetup()
        {
            if (worldSetupDone) return;

            markerLayer = LayerMask.NameToLayer(MarkerLayerName);
            EnsureOverlayCamera();

            // 从 Screen Overlay Canvas 里拆出来，否则永远是贴在 UI 上的死图
            if (IsUnderScreenOverlay())
                transform.SetParent(null, true);

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.enabled = true;
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = overlayCam != null ? overlayCam : UnityEngine.Camera.main;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            transform.localScale = Vector3.one * WorldScale;

            if (markerLayer >= 0)
                SetLayerRecursively(transform, markerLayer);

            worldSetupDone = true;
        }

        private bool IsUnderScreenOverlay()
        {
            Transform p = transform.parent;
            while (p != null)
            {
                Canvas c = p.GetComponent<Canvas>();
                if (c != null && c.renderMode == RenderMode.ScreenSpaceOverlay)
                    return true;
                p = p.parent;
            }
            return false;
        }

        private void EnsureOverlayCamera()
        {
            if (overlayCam != null) return;

            UnityEngine.Camera main = UnityEngine.Camera.main;
            if (main == null) return;

            var data = main.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                for (int i = 0; i < data.cameraStack.Count; i++)
                {
                    UnityEngine.Camera stacked = data.cameraStack[i];
                    if (stacked != null && stacked.name == "LockOnOverlayCamera")
                    {
                        overlayCam = stacked;
                        overlayOwned = false;
                        ConfigureOverlayCamera(overlayCam, main);
                        return;
                    }
                }
            }

            GameObject go = new GameObject("LockOnOverlayCamera");
            go.transform.SetParent(main.transform, false);
            overlayCam = go.AddComponent<UnityEngine.Camera>();
            overlayOwned = true;
            ConfigureOverlayCamera(overlayCam, main);

            if (data != null && !data.cameraStack.Contains(overlayCam))
                data.cameraStack.Add(overlayCam);
        }

        private void ConfigureOverlayCamera(UnityEngine.Camera cam, UnityEngine.Camera main)
        {
            cam.fieldOfView = main.fieldOfView;
            cam.orthographic = main.orthographic;
            cam.orthographicSize = main.orthographicSize;
            cam.nearClipPlane = main.nearClipPlane;
            cam.farClipPlane = main.farClipPlane;
            cam.allowHDR = main.allowHDR;
            cam.allowMSAA = false;
            cam.cullingMask = markerLayer >= 0 ? (1 << markerLayer) : 0;
            cam.clearFlags = CameraClearFlags.Nothing;
            cam.depth = main.depth + 1;
            cam.enabled = true;

            var overlayData = cam.GetUniversalAdditionalCameraData();
            overlayData.renderType = CameraRenderType.Overlay;
            overlayData.renderPostProcessing = false;

            if (markerLayer >= 0)
                main.cullingMask &= ~(1 << markerLayer);
        }

        private void TearDownOverlayCamera()
        {
            if (!overlayOwned || overlayCam == null) return;

            UnityEngine.Camera main = UnityEngine.Camera.main;
            if (main != null)
            {
                var data = main.GetUniversalAdditionalCameraData();
                if (data != null && data.cameraStack.Contains(overlayCam))
                    data.cameraStack.Remove(overlayCam);
            }

            if (overlayCam != null)
                Destroy(overlayCam.gameObject);

            overlayCam = null;
            overlayOwned = false;
        }

        private void AutoBindFollowTarget()
        {
            if (followTarget != null) return;

            CharacterBody boss = null;
            if (CombatManager.Instance != null)
                boss = CombatManager.Instance.BossRef;
            if (boss == null)
            {
                BTBrain bt = FindObjectOfType<BTBrain>();
                if (bt != null) boss = bt.GetComponent<CharacterBody>();
            }
            if (boss != null)
                BindFollowTarget(boss);
        }

        private void Refresh()
        {
            ResolveRefs();
            if (!Application.isPlaying) return;

            bool showFinisher = isFinisherReady;
            bool showFocus = !isFinisherReady && isLocked;

            if (finisher != null) finisher.SetActive(showFinisher);
            if (focusOn != null) focusOn.SetActive(showFocus);
        }

        private void ResolveRefs()
        {
            if (focusOn == null)
            {
                Transform t = transform.Find("FocusOn");
                if (t != null) focusOn = t.gameObject;
            }

            if (finisher == null)
            {
                Transform t = transform.Find("Finsher");
                if (t == null) t = transform.Find("Finisher");
                if (t != null) finisher = t.gameObject;
            }
        }

        private static void SetLayerRecursively(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursively(root.GetChild(i), layer);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }

}
