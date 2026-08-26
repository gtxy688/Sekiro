using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// 生成宽刀光带材质（柄到尖扫过的绸带），挂到 FXManager。
// 用法：Tools/战斗/生成挥刀刀光
public static class AttackSlashBuilder
{
    const string SekrioFx = "Assets/Sekrio/FX";
    const string SekiroFx = SekrioFx;
    const string OutFolder = "Assets/Prefabs/FX/Slash";
    const string TrailMatPath = OutFolder + "/PlayerSwingTrail.mat";

    [MenuItem("Tools/战斗/生成挥刀刀光")]
    public static void Build()
    {
        Texture2D streak = LoadFx("s02001");
        if (streak == null) streak = LoadFx("s01010");
        if (streak == null) streak = LoadFx("s01000");
        if (streak == null)
        {
            EditorUtility.DisplayDialog("生成挥刀刀光", "Assets/Sekrio/FX 里找不到刀光贴图。", "确定");
            return;
        }

        PrepareTexture(streak);
        EnsureFolder("Assets/Prefabs", "FX");
        EnsureFolder("Assets/Prefabs/FX", "Slash");

        Shader shader = Shader.Find("ARPG/FX/SwordRibbon");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("生成挥刀刀光", "找不到 ARPG/FX/SwordRibbon shader。", "确定");
            return;
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(TrailMatPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, TrailMatPath);
        }
        else
        {
            mat.shader = shader;
        }

        mat.SetTexture("_MainTex", streak);
        mat.SetColor("_Color", Color.white);
        mat.SetFloat("_SoftEdge", 0.14f);
        mat.SetFloat("_Streak", 0.5f);
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);

        FXManager fx = Object.FindObjectOfType<FXManager>();
        if (fx == null)
        {
            GameObject go = new GameObject("FXManager");
            Undo.RegisterCreatedObjectUndo(go, "Create FXManager");
            fx = go.AddComponent<FXManager>();
        }

        Undo.RecordObject(fx, "Assign sword ribbon");
        fx.playerSwingTrailMat = mat;
        fx.playerSlashFx = null;
        EditorUtility.SetDirty(fx);
        AssetDatabase.SaveAssets();
        Selection.activeObject = fx;

        EditorUtility.DisplayDialog(
            "生成挥刀刀光",
            "已改成宽刀光带（沿刀刃扫过的半透明绸带）。\nPlay 后玩家出刀即可，不再闪新月贴图。",
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
            if (parts.Length >= 2)
            {
                string acc = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = acc + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(acc, parts[i]);
                    acc = next;
                }
            }
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
        if (importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
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
}
