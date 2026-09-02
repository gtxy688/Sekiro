using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using ARPG.Mgr;
namespace ARPG.Editor
{

    // 生成刀尖细线拖尾材质，挂到 FXManager。
    // 用法：Tools/战斗/生成挥刀刀光
    public static class AttackSlashBuilder
    {
        // 「Sekrio」是资源目录的实际拼写（原名如此，不是笔误修正项）。
        // 别再补一个 "Assets/Sekiro/FX" 的 fallback——那个目录不存在，搜它只是白跑一趟。
        const string SekrioFx = "Assets/Sekrio/FX";
        const string OutFolder = "Assets/Prefabs/FX/Slash";
        const string TrailMatPath = OutFolder + "/PlayerSwingTrail.mat";

        [MenuItem("Tools/战斗/生成挥刀刀光")]
        public static void Build()
        {
            Texture2D streak = LoadFx("s01000");
            if (streak == null) streak = LoadFx("s01005");
            if (streak == null) streak = LoadFx("s02001");
            if (streak != null)
                PrepareTexture(streak);

            EnsureFolder("Assets/Prefabs", "FX");
            EnsureFolder("Assets/Prefabs/FX", "Slash");

            Shader shader = Shader.Find("ARPG/FX/AdditiveSpark");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                EditorUtility.DisplayDialog("生成挥刀刀光", "找不到 AdditiveSpark / Sprites/Default。", "确定");
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

            mat.SetTexture("_MainTex", streak != null ? streak : Texture2D.whiteTexture);
            mat.SetColor("_Color", Color.white);
            if (mat.HasProperty("_Cutoff"))
                mat.SetFloat("_Cutoff", 0.08f);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);

            FXManager fx = Object.FindObjectOfType<FXManager>();
            if (fx == null)
            {
                GameObject go = new GameObject("FXManager");
                Undo.RegisterCreatedObjectUndo(go, "Create FXManager");
                fx = go.AddComponent<FXManager>();
            }

            Undo.RecordObject(fx, "Assign swing trail");
            fx.playerSwingTrailMat = mat;
            EditorUtility.SetDirty(fx);
            AssetDatabase.SaveAssets();
            Selection.activeObject = fx;

            EditorUtility.DisplayDialog(
                "生成挥刀刀光",
                "已改成刀尖细线拖尾，不再铺半圆扇面。\nPlay 后玩家出刀即可。",
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
    }

}
