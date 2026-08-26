using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 把白字黑底「危」PNG 做成 Boss 头顶 Billboard。
// 用法：拖进 Assets/Art/FX，再点 Tools/战斗/生成危字特效
public static class PerilousKanjiBuilder
{
    const string TexFolder = "Assets/Art/FX";
    const string PrefabFolder = "Assets/Prefabs/FX/Perilous";
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
        EnsureFolder("Assets/Prefabs/FX", "Perilous");

        Texture2D tex = FindKanjiTex();
        if (tex == null)
        {
            Texture2D selected = Selection.activeObject as Texture2D;
            if (selected != null)
            {
                string selPath = AssetDatabase.GetAssetPath(selected);
                string selName = System.IO.Path.GetFileNameWithoutExtension(selPath);
                if (selName.Contains("危"))
                    tex = selected;
            }
        }

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
            new Color(0.32f, 0.00f, 0.00f, 1f),
            new Color(0.55f, 0.04f, 0.03f, 1f),
            0.06f, 0.7f);
        Material coreMat = CreateMat(CoreMatPath, shader, tex,
            new Color(0.48f, 0.01f, 0.01f, 1f),
            new Color(0.78f, 0.08f, 0.05f, 1f),
            0.08f, 0.45f);

        GameObject root = new GameObject("PerilousWarning");
        PerilousWarningView view = root.AddComponent<PerilousWarningView>();
        MeshRenderer glow = CreateQuad(root.transform, "Glow", glowMat, GlowWidth);
        MeshRenderer core = CreateQuad(root.transform, "Core", coreMat, CoreWidth);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("glowRenderer").objectReferenceValue = glow;
        so.FindProperty("coreRenderer").objectReferenceValue = core;
        so.FindProperty("headOffset").floatValue = 0.55f;
        so.FindProperty("showDuration").floatValue = 0.8f;
        so.ApplyModifiedProperties();

        root.SetActive(false);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            EditorUtility.DisplayDialog(
                "生成危字特效",
                "保存 PerilousKanji.prefab 失败，请检查 Prefabs/FX 目录。",
                "确定");
            return;
        }

        bool assigned = PlaceInScene(prefab);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);

        string message = assigned
            ? "已生成 PerilousKanji 预制体并挂到 CombatUIController。\n场景里旧的屏幕中央「危」字已关掉。"
            : "已生成 PerilousKanji 预制体。\n场景里没有 CombatUIController，请手动把 PerilousWarning 挂到 perilousWarningView。";
        EditorUtility.DisplayDialog("生成危字特效", message, "确定");
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
        Color edge, Color core, float cutoff, float thickness)
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
        mat.SetFloat("_Thickness", thickness);
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

    static bool PlaceInScene(GameObject prefab)
    {
        PerilousWarningView[] views = Object.FindObjectsOfType<PerilousWarningView>(true);
        for (int i = 0; i < views.Length; i++)
        {
            if (views[i] == null) continue;
            if (views[i].GetComponentInParent<Canvas>() != null)
                views[i].gameObject.SetActive(false);
            else
                Undo.DestroyObjectImmediate(views[i].gameObject);
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        instance.name = "PerilousWarning";
        instance.SetActive(false);
        Undo.RegisterCreatedObjectUndo(instance, "Place PerilousWarning");

        CombatUIController controller = Object.FindObjectOfType<CombatUIController>(true);
        if (controller != null)
        {
            Undo.RecordObject(controller, "Assign perilous warning");
            SerializedObject so = new SerializedObject(controller);
            so.FindProperty("perilousWarningView").objectReferenceValue =
                instance.GetComponent<PerilousWarningView>();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);
            return true;
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
