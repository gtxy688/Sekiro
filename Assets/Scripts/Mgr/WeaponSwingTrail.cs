using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// 刀光跟刀：每帧记下真实柄/尖，段与段之间用 Slerp 圆滑过渡。
// 前缘永远是当前刀身，不绕起刀时的世界坐标点画死圆。
public class WeaponSwingTrail : MonoBehaviour
{
    const int MaxSamples = 48;
    const int MaxSpokes = 96;
    const float FadeOut = 0.22f;
    const float MinStep = 0.008f;
    const float MinPath = 0.04f;
    const float MinBlade = 0.55f;

    struct Sample
    {
        public Vector3 hilt;
        public Vector3 tip;
    }

    readonly List<Sample> samples = new List<Sample>(MaxSamples);
    readonly List<Vector3> verts = new List<Vector3>(MaxSpokes * 2);
    readonly List<Vector2> uvs = new List<Vector2>(MaxSpokes * 2);
    readonly List<Color> colors = new List<Color>(MaxSpokes * 2);
    readonly List<int> tris = new List<int>(MaxSpokes * 6);

    Mesh mesh;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    Material materialInst;
    bool emitting;
    float fadeOutTime;
    Color colorStart = Color.white;
    Color colorEnd = new Color(0.92f, 0.95f, 1f, 1f);

    public void Setup(Material mat)
    {
        if (mesh != null) return;
        Shader ribbon = Shader.Find("ARPG/FX/SwordRibbon");
        if (ribbon == null && mat == null) return;

        TrailRenderer leftover = GetComponent<TrailRenderer>();
        if (leftover != null)
            leftover.enabled = false;

        GameObject go = new GameObject("SwordRibbon");
        go.hideFlags = HideFlags.DontSave;
        go.transform.SetParent(null);
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        go.transform.localScale = Vector3.one;
        meshFilter = go.AddComponent<MeshFilter>();
        meshRenderer = go.AddComponent<MeshRenderer>();
        mesh = new Mesh { name = "SwordRibbon" };
        mesh.MarkDynamic();
        meshFilter.sharedMesh = mesh;
        materialInst = ribbon != null ? new Material(ribbon) : new Material(mat);
        materialInst.SetTexture("_MainTex", Texture2D.whiteTexture);
        materialInst.SetColor("_Color", Color.white);
        if (materialInst.HasProperty("_SoftEdge"))
            materialInst.SetFloat("_SoftEdge", 0.16f);
        if (materialInst.HasProperty("_Streak"))
            materialInst.SetFloat("_Streak", 0.2f);
        materialInst.renderQueue = 3000;
        meshRenderer.sharedMaterial = materialInst;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.enabled = false;
    }

    public void Begin(Color start, Color end)
    {
        if (mesh == null) return;
        colorStart = start;
        colorEnd = end;
        samples.Clear();
        emitting = true;
        Capture(true);
    }

    public void End()
    {
        emitting = false;
        fadeOutTime = Time.time;
    }

    void LateUpdate()
    {
        if (mesh == null) return;

        if (meshFilter != null)
        {
            Transform t = meshFilter.transform;
            t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            t.localScale = Vector3.one;
        }

        if (emitting)
            Capture(false);
        else if (samples.Count > 0 && Time.time - fadeOutTime > FadeOut)
            samples.Clear();

        Rebuild();
    }

    void Capture(bool force)
    {
        GetBlade(out Vector3 hilt, out Vector3 tip);
        if (samples.Count == 0)
        {
            samples.Add(new Sample { hilt = hilt, tip = tip });
            return;
        }

        Sample last = samples[samples.Count - 1];
        float moved = (tip - last.tip).sqrMagnitude + (hilt - last.hilt).sqrMagnitude;
        float ang = Vector3.Angle(tip - hilt, last.tip - last.hilt);
        if (!force && moved < MinStep * MinStep && ang < 1.2f)
        {
            last.hilt = hilt;
            last.tip = tip;
            samples[samples.Count - 1] = last;
            return;
        }

        if (samples.Count >= MaxSamples)
            samples.RemoveAt(1);

        samples.Add(new Sample { hilt = hilt, tip = tip });
    }

    void GetBlade(out Vector3 hilt, out Vector3 tip)
    {
        tip = transform.position;
        hilt = transform.parent != null ? transform.parent.position : tip - transform.up * MinBlade;
        Vector3 along = tip - hilt;
        if (along.sqrMagnitude < 0.0001f)
            along = transform.up;
        along.Normalize();
        if (Vector3.Distance(hilt, tip) < MinBlade)
            hilt = tip - along * MinBlade;
    }

    void Rebuild()
    {
        if (!TryBuildRibbon())
        {
            mesh.Clear();
            if (meshRenderer != null)
                meshRenderer.enabled = false;
        }
    }

    bool TryBuildRibbon()
    {
        int n = samples.Count;
        if (n < 2) return false;

        float path = 0f;
        for (int i = 1; i < n; i++)
            path += (samples[i].tip - samples[i - 1].tip).magnitude;
        if (path < MinPath)
            return false;

        float globalFade = 1f;
        if (!emitting)
            globalFade = 1f - Mathf.Clamp01((Time.time - fadeOutTime) / FadeOut);

        verts.Clear();
        uvs.Clear();
        colors.Clear();
        tris.Clear();

        int spokes = 0;
        for (int i = 0; i < n - 1; i++)
        {
            Sample a = samples[i];
            Sample b = samples[i + 1];
            Vector3 d0 = a.tip - a.hilt;
            Vector3 d1 = b.tip - b.hilt;
            float len0 = d0.magnitude;
            float len1 = d1.magnitude;
            if (len0 < 0.001f || len1 < 0.001f)
                continue;

            int div = Mathf.Clamp(Mathf.RoundToInt(Vector3.Angle(d0, d1) / 2.5f), 1, 8);
            int s0 = i == 0 ? 0 : 1;
            for (int s = s0; s <= div; s++)
            {
                if (spokes >= MaxSpokes)
                    break;
                float u = s / (float)div;
                float t = (i + u) / (n - 1);
                Vector3 hilt = Vector3.Lerp(a.hilt, b.hilt, u);
                Vector3 dir = SlerpDir(d0, d1, u);
                float len = Mathf.Lerp(len0, len1, u);
                AddSpoke(hilt, hilt + dir * len, t, globalFade);
                spokes++;
            }
            if (spokes >= MaxSpokes)
                break;
        }

        if (spokes < 2)
            return false;

        if (emitting)
        {
            GetBlade(out Vector3 liveHilt, out Vector3 liveTip);
            int lastH = (spokes - 1) * 2;
            verts[lastH] = liveHilt;
            verts[lastH + 1] = liveTip;
        }

        for (int i = 0; i < spokes - 1; i++)
        {
            int i0 = i * 2;
            int i1 = i0 + 1;
            int i2 = i0 + 2;
            int i3 = i0 + 3;
            tris.Add(i0);
            tris.Add(i1);
            tris.Add(i2);
            tris.Add(i1);
            tris.Add(i3);
            tris.Add(i2);
        }

        mesh.Clear();
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        meshRenderer.enabled = true;
        return true;
    }

    void AddSpoke(Vector3 hilt, Vector3 tip, float t, float fade)
    {
        Color rgb = Color.Lerp(colorEnd, colorStart, t);
        rgb.a *= fade * Mathf.SmoothStep(0.45f, 1f, t);
        verts.Add(hilt);
        verts.Add(tip);
        uvs.Add(new Vector2(t, 0f));
        uvs.Add(new Vector2(t, 1f));
        colors.Add(rgb);
        colors.Add(rgb);
    }

    static Vector3 SlerpDir(Vector3 a, Vector3 b, float t)
    {
        Vector3 na = a.normalized;
        Vector3 nb = b.normalized;
        if (Vector3.Dot(na, nb) < -0.98f)
            return Vector3.Slerp(na, nb, t);
        return Vector3.Slerp(na, nb, t).normalized;
    }

    void OnDestroy()
    {
        if (meshFilter != null && meshFilter.gameObject != null)
            Destroy(meshFilter.gameObject);
        if (mesh != null)
            Destroy(mesh);
        if (materialInst != null)
            Destroy(materialInst);
    }
}
