using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using ARPG.Mgr;
namespace ARPG.Editor
{

    // 用 Assets/Sekrio/FX 里的星光/光环/拉丝点，生成多层格挡火花。
    // 用法：Tools/战斗/生成格挡火花
    public static class DeflectSparkBuilder
    {
        const string SekrioFx = "Assets/Sekrio/FX";
        const string SekiroFx = SekrioFx;
        const string OutFolder = "Assets/Prefabs/FX";
        const string NormalPath = OutFolder + "/BlockSparks_Normal.prefab";
        const string PerfectPath = OutFolder + "/BlockSparks_Perfect.prefab";

        [MenuItem("Tools/战斗/生成格挡火花")]
        public static void Build()
        {
            Texture2D flashSmall = LoadFx("s01004");
            Texture2D flashBig = LoadFx("s01003");
            Texture2D glint = LoadFx("s01009");
            Texture2D ring = LoadFx("s01008");
            Texture2D slash = LoadFx("s02012");
            Texture2D dot = LoadFx("s01000");
            if (dot == null) dot = LoadFx("s01005");

            if (flashSmall == null && flashBig == null)
            {
                EditorUtility.DisplayDialog(
                    "生成格挡火花",
                    "Assets/Sekrio/FX 里找不到 s01003 / s01004。",
                    "确定");
                return;
            }

            if (flashSmall == null) flashSmall = flashBig;
            if (flashBig == null) flashBig = flashSmall;

            PrepareTexture(flashSmall);
            PrepareTexture(flashBig);
            PrepareTexture(glint);
            PrepareTexture(ring);
            PrepareTexture(slash);
            PrepareTexture(dot);

            EnsureFolder("Assets/Prefabs", "FX");

            Color gold = new Color(1f, 0.55f, 0.12f, 1f);
            Color pale = new Color(1f, 0.92f, 0.45f, 1f);

            GameObject normal = BuildPrefab(
                "BlockSparks_Normal", pale, flashSmall, glint, ring, slash, dot,
                streakCount: 16, streakSpeed: 7f, flashSize: 0.7f, withSlash: false);
            GameObject perfect = BuildPrefab(
                "BlockSparks_Perfect", gold, flashBig, glint, ring, slash, dot,
                streakCount: 36, streakSpeed: 12f, flashSize: 1.15f, withSlash: true);

            PrefabUtility.SaveAsPrefabAsset(normal, NormalPath);
            PrefabUtility.SaveAsPrefabAsset(perfect, PerfectPath);
            Object.DestroyImmediate(normal);
            Object.DestroyImmediate(perfect);

            FXManager fx = Object.FindObjectOfType<FXManager>();
            if (fx == null)
            {
                GameObject go = new GameObject("FXManager");
                Undo.RegisterCreatedObjectUndo(go, "Create FXManager");
                fx = go.AddComponent<FXManager>();
            }

            Undo.RecordObject(fx, "Assign deflect sparks");
            fx.normalBlockSparks = AssetDatabase.LoadAssetAtPath<GameObject>(NormalPath);
            fx.perfectParrySparks = AssetDatabase.LoadAssetAtPath<GameObject>(PerfectPath);
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = fx;

            EditorUtility.DisplayDialog(
                "生成格挡火花",
                "已用 Sekrio/FX 贴图生成多层火花（中心闪光 + 放射火星 + 光环）。\n已挂到 FXManager。",
                "确定");
        }

        static Texture2D LoadFx(string name)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(SekrioFx + "/" + name + ".png");
            if (tex == null)
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sekrio/" + name + ".png");
            return tex;
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

        static void PrepareTexture(Texture2D tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool dirty = false;
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                dirty = true;
            }
            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                dirty = true;
            }
            TextureImporterAlphaSource want = importer.DoesSourceTextureHaveAlpha()
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.FromGrayScale;
            if (importer.alphaSource != want)
            {
                importer.alphaSource = want;
                dirty = true;
            }
            if (dirty)
                importer.SaveAndReimport();
        }

        static Material MakeMat(string assetName, Texture2D tex, Color tint, float cutoff)
        {
            string path = OutFolder + "/" + assetName + ".mat";
            Shader shader = Shader.Find("ARPG/FX/AdditiveSpark");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

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
            mat.SetColor("_Color", tint);
            mat.SetFloat("_Cutoff", cutoff);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static GameObject BuildPrefab(
            string name, Color tint, Texture2D flashTex, Texture2D glintTex, Texture2D ringTex,
            Texture2D slashTex, Texture2D dotTex,
            short streakCount, float streakSpeed, float flashSize, bool withSlash)
        {
            GameObject root = new GameObject(name);
            ParticleSystem life = root.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule lifeMain = life.main;
            lifeMain.duration = 0.4f;
            lifeMain.loop = false;
            lifeMain.playOnAwake = true;
            lifeMain.maxParticles = 1;
            lifeMain.startLifetime = 0.4f;
            lifeMain.startSize = 0f;
            lifeMain.startSpeed = 0f;
            lifeMain.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule lifeEm = life.emission;
            lifeEm.rateOverTime = 0f;
            lifeEm.SetBursts(new[] { new ParticleSystem.Burst(0f, 0) });
            ParticleSystemRenderer lifeRend = root.GetComponent<ParticleSystemRenderer>();
            lifeRend.enabled = false;

            Material flashMat = MakeMat(name + "_Flash", flashTex, Color.white, 0.1f);
            Material glintMat = glintTex != null ? MakeMat(name + "_Glint", glintTex, Color.white, 0.08f) : flashMat;
            Material ringMat = ringTex != null ? MakeMat(name + "_Ring", ringTex, tint, 0.06f) : null;
            Material slashMat = slashTex != null ? MakeMat(name + "_Slash", slashTex, tint, 0.05f) : null;
            Material streakMat = dotTex != null ? MakeMat(name + "_Streak", dotTex, tint, 0.05f) : flashMat;

            AddBillboard(root, "Flash", flashMat, flashSize * 0.85f, flashSize * 1.2f, 0.14f, 1);
            AddBillboard(root, "Glint", glintMat, flashSize * 0.45f, flashSize * 0.75f, 0.12f, 1);
            if (ringMat != null)
                AddRing(root, "Ring", ringMat, flashSize * 0.5f, flashSize * 1.8f);
            if (withSlash && slashMat != null)
                AddSlash(root, "Slash", slashMat, flashSize * 2.4f);
            AddStreaks(root, "Streaks", streakMat, streakCount, streakSpeed);

            return root;
        }

        static ParticleSystem AddChild(GameObject root, string childName)
        {
            GameObject child = new GameObject(childName);
            child.transform.SetParent(root.transform, false);
            return child.AddComponent<ParticleSystem>();
        }

        static void CommonMain(ParticleSystem ps, float lifetime, int max)
        {
            ParticleSystem.MainModule main = ps.main;
            main.duration = lifetime;
            main.loop = false;
            main.playOnAwake = true;
            main.startDelay = 0f;
            main.startLifetime = lifetime;
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = max;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
        }

        static void FadeColor(ParticleSystem ps, Color mid)
        {
            ParticleSystem.ColorOverLifetimeModule colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(mid, 0.35f),
                    new GradientColorKey(new Color(mid.r * 0.6f, mid.g * 0.35f, mid.b * 0.1f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.2f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOver.color = g;
        }

        static void BindMat(ParticleSystem ps, Material mat, ParticleSystemRenderMode mode)
        {
            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        static void AddBillboard(GameObject root, string name, Material mat, float sizeMin, float sizeMax, float life, short burst)
        {
            ParticleSystem ps = AddChild(root, name);
            CommonMain(ps, life, burst);
            ParticleSystem.MainModule main = ps.main;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, burst) });
            ParticleSystem.ShapeModule billboardShape = ps.shape;
            billboardShape.enabled = false;
            FadeColor(ps, Color.white);

            ParticleSystem.SizeOverLifetimeModule sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f, 0.4f),
                new Keyframe(0.12f, 1f),
                new Keyframe(1f, 0.2f)));

            BindMat(ps, mat, ParticleSystemRenderMode.Billboard);
        }

        static void AddRing(GameObject root, string name, Material mat, float from, float to)
        {
            ParticleSystem ps = AddChild(root, name);
            CommonMain(ps, 0.18f, 1);
            ParticleSystem.MainModule main = ps.main;
            main.startSpeed = 0f;
            main.startSize = from;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule ringShape = ps.shape;
            ringShape.enabled = false;
            FadeColor(ps, mat.GetColor("_Color"));

            ParticleSystem.SizeOverLifetimeModule sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            float ratio = to / Mathf.Max(0.01f, from);
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, ratio));

            BindMat(ps, mat, ParticleSystemRenderMode.Billboard);
        }

        static void AddSlash(GameObject root, string name, Material mat, float width)
        {
            ParticleSystem ps = AddChild(root, name);
            CommonMain(ps, 0.1f, 1);
            ParticleSystem.MainModule main = ps.main;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = width;
            main.startSizeY = width * 0.08f;
            main.startSizeZ = 1f;
            main.startRotation = 0f;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });
            ParticleSystem.ShapeModule slashShape = ps.shape;
            slashShape.enabled = false;
            FadeColor(ps, Color.white);
            BindMat(ps, mat, ParticleSystemRenderMode.Billboard);
        }

        static void AddStreaks(GameObject root, string name, Material mat, short count, float speed)
        {
            ParticleSystem ps = AddChild(root, name);
            CommonMain(ps, 0.22f, count);
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.45f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
            main.startColor = Color.white;
            ps.emission.SetBursts(new[] { new ParticleSystem.Burst(0f, count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;
            shape.radiusThickness = 1f;

            FadeColor(ps, mat.GetColor("_Color"));

            ParticleSystem.SizeOverLifetimeModule sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.15f));

            ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 3.2f;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

}
