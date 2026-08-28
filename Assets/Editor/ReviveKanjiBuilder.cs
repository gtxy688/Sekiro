using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 回生.png 直接当图片画：亮度当透明，位置与治字相同。
public static class ReviveKanjiBuilder
{
    const string TexPath = "Assets/Prefabs/FX/Respawn/回生.png";
    const string PrefabFolder = "Assets/Prefabs/FX/Respawn";
    const string CoreMatPath = PrefabFolder + "/ReviveKanji_Core.mat";
    const string PrefabPath = PrefabFolder + "/ReviveKanji.prefab";

    const float CoreWidth = 0.42f;

    [MenuItem("Tools/战斗/生成回生特效")]
    public static void Build()
    {
        string message;
        bool ok = TryBuild(out message);
        EditorUtility.DisplayDialog("生成回生特效", message, "确定");
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
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);
        if (tex == null)
        {
            message = "找不到「回生.png」。请放到 Assets/Prefabs/FX/Respawn。";
            return false;
        }

        PrepareTexture(tex);
        tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexPath);

        Shader shader = Shader.Find("ARPG/FX/HealSprite");
        if (shader == null)
        {
            message = "找不到 Shader ARPG/FX/HealSprite。";
            return false;
        }

        Color tint = new Color(0.96f, 0.88f, 0.62f, 1f);
        Material coreMat = CreateMat(CoreMatPath, shader, tex, tint);

        GameObject root = new GameObject("ReviveKanji");
        ReviveKanjiView view = root.AddComponent<ReviveKanjiView>();
        float height = CoreWidth;
        if (tex != null && tex.width > 0)
            height = CoreWidth * ((float)tex.height / tex.width);
        MeshRenderer core = CreateQuad(root.transform, "Core", coreMat, CoreWidth, height);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("glyphRenderer").objectReferenceValue = core;
        so.FindProperty("tint").colorValue = tint;
        so.FindProperty("headOffset").floatValue = -0.4f;
        so.FindProperty("showDuration").floatValue = 0.8f;
        so.ApplyModifiedProperties();

        root.SetActive(true);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            message = "保存 ReviveKanji.prefab 失败。";
            return false;
        }

        bool assigned = PlaceInScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        message = assigned
            ? "已用 回生.png 生成 Billboard，并挂到场景里。"
            : "已生成预制体，但没找到 CombatUIController。";
        return assigned;
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

    static MeshRenderer CreateQuad(Transform parent, string name, Material mat, float width, float height)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new Vector3(width, height, width);

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
        ReviveKanjiView[] views = Object.FindObjectsOfType<ReviveKanjiView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            Undo.DestroyObjectImmediate(views[i].gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "ReviveKanji";
        instance.SetActive(true);
        Undo.RegisterCreatedObjectUndo(instance, "Place ReviveKanji");

        GameObject playerGo = GameObject.Find("Player");
        CharacterBody player = playerGo != null
            ? playerGo.GetComponent<CharacterBody>()
            : Object.FindObjectOfType<CharacterBody>(true);
        ReviveKanjiView view = instance.GetComponent<ReviveKanjiView>();
        if (view != null && player != null)
            view.BindFollowTarget(player);

        CombatUIController controller = Object.FindObjectOfType<CombatUIController>(true);
        if (controller != null)
        {
            Undo.RecordObject(controller, "Assign revive kanji");
            SerializedObject so = new SerializedObject(controller);
            SerializedProperty prop = so.FindProperty("reviveKanjiView");
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
}
