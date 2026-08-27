using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 治.png 直接当图片画：亮度当透明，不走危字加法 Shader。
public static class HealKanjiBuilder
{
    const string PreferredTexA = "Assets/Sekrio/FX/治.png";
    const string PreferredTexB = "Assets/Sekiro/FX/治.png";
    const string PrefabFolder = "Assets/Prefabs/FX/Heal";
    const string CoreMatPath = PrefabFolder + "/HealKanji_Core.mat";
    const string PrefabPath = PrefabFolder + "/HealKanji.prefab";

    const float CoreWidth = 0.60f;

    [MenuItem("Tools/战斗/生成治愈特效")]
    public static void Build()
    {
        string message;
        bool ok = TryBuild(out message);
        EditorUtility.DisplayDialog("生成治愈特效", message, "确定");
        if (ok)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
        }
    }

    public static bool TryBuild(out string message)
    {
        EnsureFolder("Assets/Prefabs", "FX");
        EnsureFolder("Assets/Prefabs/FX", "Heal");

        Texture2D tex = FindKanjiTex();
        if (tex == null)
        {
            message = "找不到「治.png」。请放到 Assets/Sekrio/FX（或 Assets/Sekiro/FX）。";
            return false;
        }

        PrepareTexture(tex);

        Shader shader = Shader.Find("ARPG/FX/HealSprite");
        if (shader == null)
        {
            message = "找不到 Shader ARPG/FX/HealSprite。";
            return false;
        }

        Color tint = new Color(0.18f, 0.92f, 0.32f, 1f);
        Material coreMat = CreateMat(CoreMatPath, shader, tex, tint);

        GameObject root = new GameObject("HealKanji");
        HealKanjiView view = root.AddComponent<HealKanjiView>();
        MeshRenderer core = CreateQuad(root.transform, "Core", coreMat, CoreWidth);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("glyphRenderer").objectReferenceValue = core;
        so.FindProperty("tint").colorValue = tint;
        so.FindProperty("headOffset").floatValue = 0.55f;
        so.FindProperty("showDuration").floatValue = 0.8f;
        so.ApplyModifiedProperties();

        root.SetActive(true);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            message = "保存 HealKanji.prefab 失败。";
            return false;
        }

        bool assigned = PlaceInScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        message = assigned
            ? "已用 治.png 生成 Billboard，并挂到场景里。"
            : "已生成预制体，但没找到 CombatUIController。";
        return assigned;
    }

    static Texture2D FindKanjiTex()
    {
        Texture2D preferred = AssetDatabase.LoadAssetAtPath<Texture2D>(PreferredTexA);
        if (preferred != null) return preferred;
        preferred = AssetDatabase.LoadAssetAtPath<Texture2D>(PreferredTexB);
        if (preferred != null) return preferred;

        string[] folders = { "Assets/Sekrio/FX", "Assets/Sekiro/FX", "Assets/Art/FX", PrefabFolder };
        for (int f = 0; f < folders.Length; f++)
        {
            if (!AssetDatabase.IsValidFolder(folders[f])) continue;
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folders[f] });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!name.Contains("治")) continue;
                return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
        }

        return null;
    }

    static void PrepareTexture(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        BakeLuminanceMaskPng(path);

        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.sRGBTexture = true;
        importer.alphaSource = TextureImporterAlphaSource.None;
        importer.alphaIsTransparency = false;
        importer.isReadable = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        TextureImporterPlatformSettings plat = importer.GetDefaultPlatformTextureSettings();
        plat.format = TextureImporterFormat.RGB24;
        plat.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(plat);
        importer.SaveAndReimport();
    }

    // 有的治.png 是「白 RGB + Alpha 字形」。看起来像黑底，但 Shader 读亮度会变成实心方块。
    static void BakeLuminanceMaskPng(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.isReadable = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        Texture2D src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (src == null) return;
        Color[] px = src.GetPixels();
        bool changed = false;
        for (int i = 0; i < px.Length; i++)
        {
            float lum = Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) * px[i].a;
            if (Mathf.Abs(px[i].r - lum) > 0.01f || px[i].a < 0.99f)
                changed = true;
            px[i] = new Color(lum, lum, lum, 1f);
        }
        if (!changed) return;

        Texture2D dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        dst.SetPixels(px);
        dst.Apply();
        System.IO.File.WriteAllBytes(path, dst.EncodeToPNG());
        Object.DestroyImmediate(dst);
        AssetDatabase.ImportAsset(path);
    }

    static Material CreateMat(string path, Shader shader, Texture2D tex, Color tint)
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
        mat.SetColor("_Color", tint);
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

    static bool PlaceInScene(GameObject prefab)
    {
        HealKanjiView[] views = Object.FindObjectsOfType<HealKanjiView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            if (views[i].GetComponentInParent<Canvas>() != null)
                views[i].gameObject.SetActive(false);
            else
                Undo.DestroyObjectImmediate(views[i].gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "HealKanji";
        instance.SetActive(true);
        Undo.RegisterCreatedObjectUndo(instance, "Place HealKanji");

        GameObject playerGo = GameObject.Find("Player");
        CharacterBody player = playerGo != null
            ? playerGo.GetComponent<CharacterBody>()
            : Object.FindObjectOfType<CharacterBody>(true);
        HealKanjiView view = instance.GetComponent<HealKanjiView>();
        if (view != null && player != null)
            view.BindFollowTarget(player);

        CombatUIController controller = Object.FindObjectOfType<CombatUIController>(true);
        if (controller != null)
        {
            Undo.RecordObject(controller, "Assign heal kanji");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty prop = so.FindProperty("healKanjiView");
            if (prop != null)
            {
                prop.objectReferenceValue = view;
                so.ApplyModifiedProperties();
            }
            EditorUtility.SetDirty(controller);
            return prop != null;
        }

        return false;
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
