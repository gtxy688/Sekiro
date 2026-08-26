# 危字 Billboard 特效 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法跟踪进度。

**目标：** Boss 危字招式时，在 Boss 头顶弹出原版发光红「危」字 Billboard（约 0.8 秒淡出），不再出现在玩家头上或屏幕正中。

**架构：** 沿用 `OnPerilousAttack`。`CombatUIController` 把跟随目标绑到 Boss。`PerilousWarningView` 用世界空间两层 Quad + 加法 Shader（亮度当遮罩），`LateUpdate` 钉头骨、朝向相机。编辑器菜单生成材质/预制体，对标格挡火花 Builder。

**技术栈：** Unity 2022 LTS、URP、DoTween、现有 CombatEventBus。本项目无自动化测试；每个任务用 Unity 编译 0 error + 手测验证。不要新建 Test Runner 程序集。不要擅自 git commit（仅当用户本会话明确要求时才提交）。

**规格：** `Docs/superpowers/specs/2026-08-26-perilous-kanji-vfx-design.md`  
**本任务只改这两份架构文档：** `Docs/architecture/06-presentation.md`、`Docs/architecture/06-presentation-test.md`  
**不要改：** `03-hit-detection.md`、`AttackState`、`CombatEventBus` 签名、危字 Hit 结算。

**现有 API（必须按此写，不要发明别名）：**

| 用途 | 名字 |
| --- | --- |
| 危字事件 | `CombatEventBus.OnPerilousAttack` / `TriggerPerilousAttack(PerilousType)` |
| UI 基类 | `UIView.Show()` / `Hide()` / `OnViewInit()` |
| Controller | `CombatUIController` 字段 `bossBody`、`perilousWarningView` |
| 锁定跟随（抄查找骨骼） | `LockOnIndicatorView` 的 `FindDeep` |
| 加法 FX 参考 | `Assets/Shaders/FX/AdditiveSpark.shader` |
| Builder 参考 | `Assets/Editor/DeflectSparkBuilder.cs` |
| DoTween | `DG.Tweening` |

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Shaders/FX/PerilousKanji.shader` | 白字黑底 → 发光红；`ZTest Always`；`_Intensity` 淡出 |
| 创建 `Assets/Editor/PerilousKanjiBuilder.cs` | 菜单生成材质、两层 Quad 预制体、挂到 Controller |
| 修改 `Assets/Scripts/UI/Views/PerilousWarningView.cs` | 重写：跟 Boss 头、Billboard、DoTween 0.8s |
| 修改 `Assets/Scripts/UI/CombatUIController.cs` | 弹出前 `BindFollowTarget(bossBody)` |
| 修改 `Assets/Editor/CombatHUDBuilder.cs` | 不再生成屏幕中央 TMP「危」；复用场景里已有 View |
| 修改 `Docs/architecture/06-presentation.md` | 危字 = Boss 头顶 Billboard |
| 修改 `Docs/architecture/06-presentation-test.md` | 恢复 #14 视觉验收 |
| 用户放入 `Assets/Art/FX/危.png` | Builder 的输入；不要手写 PNG |
| Builder 生成 `Assets/Prefabs/FX/PerilousKanji.prefab` 与 `.mat` | 不要手写 YAML Prefab |

不要做：粒子、Bloom、改事件签名、Overlay 相机、按 `PerilousType` 换图标。

---

### 任务 1：发光红 Shader

**文件：**
- 创建：`Assets/Shaders/FX/PerilousKanji.shader`

- [ ] **步骤 1：写入下列完整 Shader（名字禁止改）**

```shader
Shader "ARPG/FX/PerilousKanji"
{
    Properties
    {
        _MainTex ("Kanji", 2D) = "white" {}
        _EdgeColor ("Edge", Color) = (0.75, 0.04, 0.04, 1)
        _CoreColor ("Core", Color) = (1, 0.45, 0.38, 1)
        _Cutoff ("Dark Crush", Range(0, 0.4)) = 0.08
        _Intensity ("Intensity", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }
        Blend One One
        Cull Off
        ZWrite Off
        ZTest Always
        Lighting Off

        Pass
        {
            Name "Kanji"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _EdgeColor;
            fixed4 _CoreColor;
            float _Cutoff;
            float _Intensity;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            // 白字黑底：亮度当遮罩。mask 高的地方走芯色（亮红），低的走边色（深红）。
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float lum = max(max(tex.r, tex.g), tex.b);
                float mask = max(lum, tex.a);
                mask = saturate((mask - _Cutoff) / max(1e-4, 1.0 - _Cutoff));
                float3 rgb = lerp(_EdgeColor.rgb, _CoreColor.rgb, mask) * mask * _Intensity;
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}
```

- [ ] **步骤 2：在 Unity 确认编译**

回到 Editor 等导入。Console 0 error。`Shader.Find("ARPG/FX/PerilousKanji")` 在后续 Builder 里能找到。

- [ ] **步骤 3：Commit（仅用户明确要求时）**

```bash
git add Assets/Shaders/FX/PerilousKanji.shader Assets/Shaders/FX/PerilousKanji.shader.meta
git commit -m "feat: add perilous kanji additive shader"
```

未要求则跳过。

---

### 任务 2：重写 PerilousWarningView

**文件：**
- 修改：`Assets/Scripts/UI/Views/PerilousWarningView.cs`（整文件替换）

字段名后续任务必须一致：`followTarget`、`headOffset`、`glowRenderer`、`coreRenderer`、`showDuration`、`BindFollowTarget`、`ShowWarning`。

- [ ] **步骤 1：用下列完整文件替换**

```csharp
using DG.Tweening;
using UnityEngine;

// "危"字警告：钉在 Boss 头顶的世界空间 Billboard，不走屏幕中央 HUD。
public class PerilousWarningView : UIView
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float headOffset = 0.35f;
    [SerializeField] private MeshRenderer glowRenderer;
    [SerializeField] private MeshRenderer coreRenderer;
    [SerializeField] private float showDuration = 0.8f;

    private const float PopDuration = 0.12f;
    private const float FadeDuration = 0.15f;

    private float intensity;
    private Tweener intensityTween;
    private Sequence showSeq;
    private MaterialPropertyBlock block;

    public override void OnViewInit()
    {
        BindRefs();
        ApplyIntensity(0f);
        Hide();
    }

    public void BindFollowTarget(CharacterBody boss)
    {
        if (boss == null) return;
        Transform found = FindDeep(boss.transform, "Head")
            ?? FindDeep(boss.transform, "Spine1")
            ?? FindDeep(boss.transform, "Spine");
        followTarget = found != null ? found : boss.transform;
    }

    public void ShowWarning(PerilousType type)
    {
        BindRefs();
        if (glowRenderer == null && coreRenderer == null)
            return;

        KillTweens();
        Show();
        BindRefs();

        transform.localScale = Vector3.zero;
        ApplyIntensity(0f);

        showSeq = DOTween.Sequence();
        showSeq.Append(transform.DOScale(Vector3.one, PopDuration).SetEase(Ease.OutBack));
        showSeq.Join(TweenIntensity(1f, 0.08f));
        float hold = Mathf.Max(0f, showDuration - PopDuration - FadeDuration);
        showSeq.AppendInterval(hold);
        showSeq.Append(transform.DOScale(Vector3.one * 0.8f, FadeDuration).SetEase(Ease.InQuad));
        showSeq.Join(TweenIntensity(0f, FadeDuration));
        showSeq.OnComplete(Hide);
    }

    private void LateUpdate()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        BindRefs();
        FollowWorld();
    }

    private void OnDisable()
    {
        KillTweens();
    }

    private void FollowWorld()
    {
        Vector3 pos = followTarget != null
            ? followTarget.position + Vector3.up * headOffset
            : transform.position;

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 to = pos - cam.transform.position;
        bool behind = Vector3.Dot(cam.transform.forward, to) <= 0f;
        SetRenderersVisible(!behind);
        if (behind) return;

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    private Tweener TweenIntensity(float to, float duration)
    {
        intensityTween = DOTween.To(() => intensity, v => ApplyIntensity(v), to, duration);
        return intensityTween;
    }

    private void ApplyIntensity(float value)
    {
        intensity = value;
        if (block == null) block = new MaterialPropertyBlock();
        block.SetFloat("_Intensity", intensity);
        if (glowRenderer != null) glowRenderer.SetPropertyBlock(block);
        if (coreRenderer != null) coreRenderer.SetPropertyBlock(block);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (glowRenderer != null) glowRenderer.enabled = visible;
        if (coreRenderer != null) coreRenderer.enabled = visible;
    }

    private void KillTweens()
    {
        if (showSeq != null && showSeq.IsActive()) showSeq.Kill();
        showSeq = null;
        if (intensityTween != null && intensityTween.IsActive()) intensityTween.Kill();
        intensityTween = null;
        transform.DOKill();
    }

    private void BindRefs()
    {
        if (glowRenderer == null)
        {
            Transform t = transform.Find("Glow");
            if (t != null) glowRenderer = t.GetComponent<MeshRenderer>();
        }
        if (coreRenderer == null)
        {
            Transform t = transform.Find("Core");
            if (t != null) coreRenderer = t.GetComponent<MeshRenderer>();
        }
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
```

`PerilousType type` 参数保留（事件仍传入），本任务不按类型换图。

- [ ] **步骤 2：Unity 编译**

Console 0 error。旧 HUD 上若还挂着这个脚本但没有 Glow/Core，`ShowWarning` 会直接 return，任务 4 生成新预制体后才看得见。

- [ ] **步骤 3：Commit（仅用户明确要求时）**

```bash
git add Assets/Scripts/UI/Views/PerilousWarningView.cs
git commit -m "feat: follow boss head for perilous kanji billboard"
```

---

### 任务 3：Controller 绑 Boss

**文件：**
- 修改：`Assets/Scripts/UI/CombatUIController.cs`

- [ ] **步骤 1：改 `HandlePerilousAttack` 为**

```csharp
    private void HandlePerilousAttack(PerilousType type)
    {
        if (perilousWarningView == null)
            BindPerilousView();
        perilousWarningView?.BindFollowTarget(bossBody);
        perilousWarningView?.ShowWarning(type);
    }
```

- [ ] **步骤 2：在 `BindPerilousView` 末尾（`OnViewInit` 之前）补上跟随绑定**

现有末尾是：

```csharp
        if (perilousWarningView == null) return;
        perilousWarningView.enabled = true;
        perilousWarningView.OnViewInit();
```

改成：

```csharp
        if (perilousWarningView == null) return;
        perilousWarningView.enabled = true;
        perilousWarningView.BindFollowTarget(bossBody);
        perilousWarningView.OnViewInit();
```

不要改事件订阅，不要改 `bossBody` 字段名。

- [ ] **步骤 3：Unity 编译，0 error**

- [ ] **步骤 4：Commit（仅用户明确要求时）**

```bash
git add Assets/Scripts/UI/CombatUIController.cs
git commit -m "fix: bind perilous warning to boss instead of player hud"
```

---

### 任务 4：编辑器生成预制体

**文件：**
- 创建：`Assets/Editor/PerilousKanjiBuilder.cs`

用户必须先把 `危.png` 放到 `Assets/Art/FX`（或在 Project 里选中该贴图）。Builder 找不到就弹窗，不要静默失败。

- [ ] **步骤 1：写入完整 Builder**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 把白字黑底「危」PNG 做成 Boss 头顶 Billboard。
// 用法：拖进 Assets/Art/FX，再点 Tools/战斗/生成危字特效
public static class PerilousKanjiBuilder
{
    const string TexFolder = "Assets/Art/FX";
    const string PrefabFolder = "Assets/Prefabs/FX";
    const string GlowMatPath = PrefabFolder + "/PerilousKanji_Glow.mat";
    const string CoreMatPath = PrefabFolder + "/PerilousKanji_Core.mat";
    const string PrefabPath = PrefabFolder + "/PerilousKanji.prefab";

    const float CoreWidth = 0.60f;
    const float GlowWidth = 0.78f;

    [MenuItem("Tools/战斗/生成危字特效")]
    public static void Build()
    {
        EnsureFolder("Assets/Art", "FX");
        EnsureFolder("Assets/Prefabs", "FX");

        Texture2D tex = FindKanjiTex();
        if (tex == null)
            tex = Selection.activeObject as Texture2D;

        if (tex == null)
        {
            EditorUtility.DisplayDialog(
                "生成危字特效",
                "先把「危.png」拖进 Assets/Art/FX（或在 Project 里选中它）再点这个菜单。",
                "确定");
            return;
        }

        PrepareTexture(tex);

        Shader shader = Shader.Find("ARPG/FX/PerilousKanji");
        if (shader == null)
        {
            EditorUtility.DisplayDialog(
                "生成危字特效",
                "找不到 Shader ARPG/FX/PerilousKanji，请确认任务 1 的 shader 已导入。",
                "确定");
            return;
        }

        Material glowMat = CreateMat(GlowMatPath, shader, tex,
            new Color(0.55f, 0.02f, 0.02f, 1f),
            new Color(0.85f, 0.12f, 0.08f, 1f),
            0.06f);
        Material coreMat = CreateMat(CoreMatPath, shader, tex,
            new Color(0.75f, 0.04f, 0.04f, 1f),
            new Color(1f, 0.45f, 0.38f, 1f),
            0.08f);

        GameObject root = new GameObject("PerilousWarning");
        PerilousWarningView view = root.AddComponent<PerilousWarningView>();
        MeshRenderer glow = CreateQuad(root.transform, "Glow", glowMat, GlowWidth);
        MeshRenderer core = CreateQuad(root.transform, "Core", coreMat, CoreWidth);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("glowRenderer").objectReferenceValue = glow;
        so.FindProperty("coreRenderer").objectReferenceValue = core;
        so.FindProperty("headOffset").floatValue = 0.35f;
        so.FindProperty("showDuration").floatValue = 0.8f;
        so.ApplyModifiedProperties();

        root.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        PlaceInScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        EditorUtility.DisplayDialog(
            "生成危字特效",
            "已生成 PerilousKanji 预制体并挂到 CombatUIController。\n场景里旧的屏幕中央「危」字已关掉。",
            "确定");
    }

    static Texture2D FindKanjiTex()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (!name.Contains("危")) continue;
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        return null;
    }

    static void PrepareTexture(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = importer.DoesSourceTextureHaveAlpha()
            ? TextureImporterAlphaSource.FromInput
            : TextureImporterAlphaSource.FromGrayScale;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    static Material CreateMat(string path, Shader shader, Texture2D tex,
        Color edge, Color core, float cutoff)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetTexture("_MainTex", tex);
        mat.SetColor("_EdgeColor", edge);
        mat.SetColor("_CoreColor", core);
        mat.SetFloat("_Cutoff", cutoff);
        mat.SetFloat("_Intensity", 1f);
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static MeshRenderer CreateQuad(Transform parent, string name, Material mat, float width)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(width, width, width);

        Collider col = go.GetComponent<Collider>();
        if (col != null) Object.DestroyImmediate(col);

        MeshRenderer mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = LightProbeUsage.Off;
        mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return mr;
    }

    static void PlaceInScene(GameObject prefab)
    {
        PerilousWarningView[] views = Object.FindObjectsOfType<PerilousWarningView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            if (views[i].GetComponentInParent<Canvas>() != null)
                views[i].gameObject.SetActive(false);
            else
                Object.DestroyImmediate(views[i].gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PerilousWarning";
        instance.SetActive(false);
        Undo.RegisterCreatedObjectUndo(instance, "Place PerilousWarning");

        CombatUIController controller = Object.FindObjectOfType<CombatUIController>(true);
        if (controller != null)
        {
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("perilousWarningView").objectReferenceValue =
                instance.GetComponent<PerilousWarningView>();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
        }
    }

    static void EnsureFolder(string parent, string name)
    {
        string path = parent + "/" + name;
        if (!AssetDatabase.IsValidFolder(parent))
        {
            string[] parts = parent.Split('/');
            if (parts.Length == 2)
                AssetDatabase.CreateFolder(parts[0], parts[1]);
        }
        if (!AssetDatabase.IsValidFolder(path))
            AssetDatabase.CreateFolder(parent, name);
    }
}
```

`PlaceInScene` 里关掉 Canvas 下旧的 `PerilousWarning`，避免两套。世界空间那份可以删了再实例化，避免重复。

- [ ] **步骤 2：用户放贴图后点菜单**

预期：`Assets/Prefabs/FX/` 出现 `PerilousKanji.prefab`、`PerilousKanji_Glow.mat`、`PerilousKanji_Core.mat`；Hierarchy 根上有未激活的 `PerilousWarning`；`CombatHUD` 的 `perilousWarningView` 指向它。

- [ ] **步骤 3：Commit（仅用户明确要求时）**

```bash
git add Assets/Editor/PerilousKanjiBuilder.cs Assets/Prefabs/FX/PerilousKanji.prefab Assets/Prefabs/FX/PerilousKanji_Glow.mat Assets/Prefabs/FX/PerilousKanji_Core.mat
git commit -m "feat: add perilous kanji prefab builder"
```

贴图若被 gitignore（`Assets/Resources/` 等）不要强行 add。`Assets/Art/FX` 不在 ignore 里则可以一起提交。

---

### 任务 5：HUD 生成器不再造中央危字

**文件：**
- 修改：`Assets/Editor/CombatHUDBuilder.cs`

- [ ] **步骤 1：删掉 `BuildCenterText(...)` 那次调用，改成复用场景 View**

把：

```csharp
        PerilousWarningView perilous = BuildCenterText(hud.transform, "PerilousWarning", "危",
            new Color(0.85f, 0.05f, 0.05f), 160);
```

换成：

```csharp
        PerilousWarningView perilous = Object.FindObjectOfType<PerilousWarningView>(true);
        if (perilous == null)
            Debug.LogWarning("[CombatHUD] 场景里没有危字 Billboard。先跑 Tools/战斗/生成危字特效。");
```

`BuildCenterText` 方法若不再被调用，整段删掉（约 208–225 行），避免以后又生成屏幕字。

- [ ] **步骤 2：Unity 编译，0 error**

- [ ] **步骤 3：Commit（仅用户明确要求时）**

```bash
git add Assets/Editor/CombatHUDBuilder.cs
git commit -m "fix: stop generating screen-center perilous text in HUD builder"
```

---

### 任务 6：表现层文档

**文件：**
- 修改：`Docs/architecture/06-presentation.md`
- 修改：`Docs/architecture/06-presentation-test.md`

- [ ] **步骤 1：在 `06-presentation.md` 里这句下面追加一小节，不要改「二、相机」编号**

```markdown
> 保留：`OnPerilousAttack`（危字提示，M17 保留）。
```

追加：

```markdown
### 危字 Billboard（M17 表现）

- 事件：`OnPerilousAttack(PerilousType)`（签名不改）。
- 位置：世界空间 Billboard，跟随 **Boss** 的 `Head`（没有则 `Spine1` / `Spine`）+ `headOffset`（默认 0.35m）。**不跟玩家、不放屏幕正中。**
- 绘制：`ARPG/FX/PerilousKanji` 加法 Shader，白字黑底当遮罩；两层 Quad（光晕 0.78m / 字形 0.60m）；`ZTest Always`。
- 时间：弹出后约 0.8 秒淡出。`LateUpdate` 只做跟随/朝向相机，不轮询战斗数值。
- 生成：`Tools/战斗/生成危字特效`。贴图放 `Assets/Art/FX`。
```

- [ ] **步骤 2：改 `06-presentation-test.md`**

删掉：

```markdown
> #14（危字 UI）已移除：M17 不在本项目范围。
```

在 M13 表 #13 后插入：

```markdown
| 14 | Boss 放突刺/横扫危字 | 红色「危」出现在 **Boss 头顶**（不在玩家头上、不在屏幕正中）；始终朝向相机；头盔挡不住；约 0.8 秒淡出 |
| 14b | 绕到 Boss 侧面后再放危字 | 字仍正对相机 |
| 14c | 连续两次危字 | 第一次被打断，重新弹出 |
| 14d | 玩家普通攻击 | 不出现危字 |
```

不要恢复 #20 危字音效（本计划不做音频）。#20 那行注释可留。

- [ ] **步骤 3：对照规格验收表，确认 06-presentation-test 覆盖规格「验收」每一行**

- [ ] **步骤 4：Commit（仅用户明确要求时）**

```bash
git add Docs/architecture/06-presentation.md Docs/architecture/06-presentation-test.md
git commit -m "docs: specify perilous kanji as boss-head billboard"
```

---

## 用户在 Unity 里要做的

1. 把 `危.png` 放进 `Assets/Art/FX`。
2. 确认任务 1–5 代码已进工程。
3. **Tools/战斗/生成危字特效**。
4. 关掉 Hierarchy 里 Canvas 下旧的 `PerilousWarning`（Builder 会关；若还在就手动关）。
5. Play → 引 Boss 放突刺/横扫危字。
6. 高低不对调 `headOffset`；大小不对调 Glow/Core 的 localScale。
7. Ctrl+S。

## 手测验收（规格原文）

| 操作 | 预期 |
|---|---|
| Boss 放突刺危字 | 红「危」在 Boss 头顶，不在玩家头上、不在屏幕正中 |
| Boss 放横扫危字 | 同样在 Boss 头顶 |
| 绕到侧面 / 背后 | 字始终正对相机 |
| 字与头盔重叠的机位 | 字仍完整可见 |
| 弹出后约 0.8 秒 | 淡出消失 |
| 连续两次危字 | 第一次被打断，重新弹出 |
| 玩家普通攻击 | 不出现危字 |

---

## 规格覆盖自检

| 规格条目 | 任务 |
|---|---|
| 跟随 Head / Spine1 / Spine + 0.35m | 2、3 |
| 世界两层 Quad、朝向相机 | 2、4 |
| ZTest Always 加法、不新建 Overlay 相机 | 1 |
| 相机背后隐藏 | 2 `behind` |
| 0.8s 弹出淡出、新事件打断 | 2 `showDuration` / `KillTweens` |
| 只跟 Boss、不改事件签名 | 3 |
| 白字黑底遮罩、芯亮边深 | 1、4 两套 mat 颜色 |
| 光晕 0.78 / 字形 0.60 | 4 `CoreWidth` / `GlowWidth` |
| Builder 找不到贴图弹窗 | 4 |
| 无骨骼则跟根 + offset | 2 `BindFollowTarget` 回退 `boss.transform` |
| View 丢失不抛异常 | 2 `ShowWarning` 无 renderer 则 return；3 空条件 |
| 关掉旧 HUD 中央字 | 4 `PlaceInScene`、5 |
| 只改 06 文档 | 6 |
| 不做粒子 / Bloom / 换图标 | 全计划未列入 |
