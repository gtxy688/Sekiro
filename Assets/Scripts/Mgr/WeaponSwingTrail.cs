using UnityEngine;
using UnityEngine.Rendering;

// 刀尖细线拖尾，跟着 Hitbox 走。不做柄到尖的半圆扇面。
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

        Shader spark = Shader.Find("ARPG/FX/AdditiveSpark");
        if (spark != null)
        {
            materialInst = new Material(spark);
            if (mat != null && mat.HasProperty("_MainTex"))
                materialInst.SetTexture("_MainTex", mat.GetTexture("_MainTex"));
            else
                materialInst.SetTexture("_MainTex", Texture2D.whiteTexture);
            materialInst.SetColor("_Color", Color.white);
            if (materialInst.HasProperty("_Cutoff"))
                materialInst.SetFloat("_Cutoff", 0.08f);
        }
        else if (mat != null)
        {
            materialInst = new Material(mat);
        }

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
        if (trail == null) return;
        ApplyColors(start, end);
        trail.Clear();
        trail.emitting = true;
    }

    public void End()
    {
        if (trail == null) return;
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
        if (materialInst != null && materialInst.HasProperty("_Color"))
            materialInst.SetColor("_Color", start);
    }

    void OnDestroy()
    {
        if (materialInst != null)
            Destroy(materialInst);
    }
}
