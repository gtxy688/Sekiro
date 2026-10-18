using UnityEngine;
using UnityEngine.Rendering;

namespace ARPG.Mgr
{

    // 在武器命中框上生成刀尖拖尾，随攻击窗口开始和停止发射。
    public class WeaponSwingTrail : MonoBehaviour
    {
        const float Duration = 0.2f;
        const float Width = 0.04f;

        TrailRenderer trail;
        Material materialInst;

        public void Setup(Material mat)
        {
            if (trail != null) return;

            trail = GetComponent<TrailRenderer>();
            if (trail == null)
                trail = gameObject.AddComponent<TrailRenderer>();

            // 使用独立材质，避免一次挥刀的颜色修改影响其他角色。
            Shader spark = Shader.Find("ARPG/FX/AdditiveSpark");
            materialInst = new Material(spark);
            if (mat != null && mat.HasProperty("_MainTex"))
                materialInst.SetTexture("_MainTex", mat.GetTexture("_MainTex"));
            else
                materialInst.SetTexture("_MainTex", Texture2D.whiteTexture);
            materialInst.SetColor("_Color", Color.white);
            materialInst.SetFloat("_Cutoff", 0.08f);

            trail.material = materialInst;
            trail.time = Duration;
            trail.minVertexDistance = 0.006f;
            trail.widthMultiplier = Width;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.7f, 0.45f),
                new Keyframe(1f, 0f));
            trail.numCornerVertices = 2;
            trail.numCapVertices = 1;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.lightProbeUsage = LightProbeUsage.Off;
            trail.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            trail.emitting = false;
            trail.Clear();
        }

        public void Begin(Color start, Color end)
        {
            ApplyColors(start, end);
            // 清除上次轨迹，避免两次攻击之间出现连线。
            trail.Clear();
            trail.emitting = true;
        }

        public void End()
        {
            trail.emitting = false;
        }

        void ApplyColors(Color start, Color end)
        {
            Gradient g = new Gradient();
            Color tip = start;
            Color tail = end;
            tail.a = Mathf.Max(0.35f, tail.a);
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(tip, 0f),
                    new GradientColorKey(tail, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.55f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            trail.colorGradient = g;
            materialInst.SetColor("_Color", start);
        }

        void OnDestroy()
        {
            if (materialInst != null)
                Destroy(materialInst);
        }
    }

}
