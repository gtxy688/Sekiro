using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ARPG.Camera
{

    // 相机避障兜底表现：镜头被墙挤到玩家身上、没有后退空间时，把玩家淡出（虚化），
    // 给镜头"让出"画面位置，避免玩家背影糊满整个屏幕。
    // 只改材质透明度与阴影，不碰碰撞体/动画；材质在首次淡出时才实例化副本，
    // 恢复时换回共享材质，避免常驻透明队列开销。由 CameraController 驱动 SetOccluded。
    public class CameraObstacleFader : MonoBehaviour
    {
        [Tooltip("淡出后的目标不透明度（0~1）。0.1 左右基本让开画面但保留轮廓不穿帮")]
        public float fadedAlpha = 0.1f;

        [Tooltip("淡出耗时（秒），要快，跟上镜头被挤过去的速度")]
        public float fadeInTime = 0.22f;

        [Tooltip("恢复耗时（秒），慢一点，避免镜头刚松开就闪一下人影")]
        public float fadeOutTime = 0.45f;

        private struct FadeEntry
        {
            public Renderer renderer;
            public Material[] shared;
            public Material[] instanced;
            public float[] originalAlpha;
            public ShadowCastingMode originalShadows;
            public bool alphaCached;
        }

        private readonly List<FadeEntry> entries = new List<FadeEntry>();
        private bool occluded;
        private float fade; // 0=正常，1=完全淡出

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP
        private static readonly int ColorId = Shader.PropertyToID("_Color");         // 内置/旧 Lit
        private static readonly int SurfaceId = Shader.PropertyToID("_Surface");
        private static readonly int BlendId = Shader.PropertyToID("_Blend");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

        public void SetOccluded(bool value)
        {
            occluded = value;
        }

        // 玩家换装/换模型后可重新收集
        public void Configure(Transform playerRoot)
        {
            RestoreAll();
            entries.Clear();
            if (playerRoot == null) return;

            foreach (Renderer r in playerRoot.GetComponentsInChildren<Renderer>(true))
            {
                // 只处理网格类渲染器；武器拖尾/粒子不淡（淡了会闪烁）
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                Material[] shared = r.sharedMaterials;
                if (shared == null || shared.Length == 0) continue;

                bool hasColor = false;
                foreach (Material m in shared)
                {
                    if (m != null && (m.HasProperty(BaseColorId) || m.HasProperty(ColorId)))
                    {
                        hasColor = true;
                        break;
                    }
                }
                if (!hasColor) continue;

                entries.Add(new FadeEntry
                {
                    renderer = r,
                    shared = shared,
                    originalAlpha = new float[shared.Length]
                });
            }
        }

        private void OnDisable()
        {
            RestoreAll();
            fade = 0f;
        }

        private void Update()
        {
            float target = occluded ? 1f : 0f;
            if (!Mathf.Approximately(fade, target))
            {
                float speed = 1f / Mathf.Max(0.01f, occluded ? fadeInTime : fadeOutTime);
                fade = Mathf.MoveTowards(fade, target, speed * Time.unscaledDeltaTime);
            }
            Apply(fade);
        }

        private void Apply(float value)
        {
            if (value <= 0.001f)
            {
                RestoreAll();
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                FadeEntry e = entries[i];

                if (e.instanced == null)
                {
                    e.instanced = new Material[e.shared.Length];
                    for (int j = 0; j < e.shared.Length; j++)
                    {
                        if (e.shared[j] == null) continue;
                        e.instanced[j] = new Material(e.shared[j]);
                        MakeFadeCapable(e.instanced[j]);
                    }
                    e.renderer.sharedMaterials = e.instanced;
                    e.originalShadows = e.renderer.shadowCastingMode;
                }

                // 虚化期间不投影，半透明影子会穿帮
                e.renderer.shadowCastingMode = ShadowCastingMode.Off;

                for (int j = 0; j < e.instanced.Length; j++)
                {
                    Material m = e.instanced[j];
                    if (m == null) continue;

                    int id = m.HasProperty(BaseColorId) ? BaseColorId : ColorId;
                    if (!e.alphaCached)
                        e.originalAlpha[j] = m.GetColor(id).a;

                    Color c = m.GetColor(id);
                    c.a = e.originalAlpha[j] * Mathf.Lerp(1f, fadedAlpha, value);
                    m.SetColor(id, c);
                }
                e.alphaCached = true;
                entries[i] = e;
            }
        }

        private void RestoreAll()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                FadeEntry e = entries[i];
                if (e.instanced == null) continue;
                e.renderer.sharedMaterials = e.shared;
                e.renderer.shadowCastingMode = e.originalShadows;
                e.instanced = null;
                e.alphaCached = false;
                entries[i] = e;
            }
        }

        // 把 Lit 材质切成 Alpha 混合（URP14 / 内置 Standard 命名都有兜底）
        private static void MakeFadeCapable(Material m)
        {
            if (m.HasProperty(SurfaceId)) m.SetFloat(SurfaceId, 1f); // 1 = Transparent
            if (m.HasProperty(BlendId)) m.SetFloat(BlendId, 0f);     // 0 = Alpha
            m.SetOverrideTag("RenderType", "Transparent");
            if (m.HasProperty(SrcBlendId))
                m.SetFloat(SrcBlendId, (float)BlendMode.SrcAlpha);
            if (m.HasProperty(DstBlendId))
                m.SetFloat(DstBlendId, (float)BlendMode.OneMinusSrcAlpha);
            if (m.HasProperty(ZWriteId)) m.SetFloat(ZWriteId, 0f);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }

}
