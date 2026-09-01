using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using ARPG.FrameWork.Body;
using ARPG.UI;
namespace ARPG.Editor
{

    // 危.png 直接当图片画：亮度当透明，与治/回生同一套 HealSprite。
    public static class PerilousKanjiBuilder
    {
        const string TexFolder = "Assets/Art/FX";
        const string PrefabFolder = "Assets/Prefabs/FX/Perilous";
        const string CoreMatPath = PrefabFolder + "/PerilousKanji_Core.mat";
        const string PrefabPath = PrefabFolder + "/PerilousKanji.prefab";

        const float CoreWidth = 0.60f;

        [MenuItem("Tools/战斗/生成危字特效")]
        public static void Build()
        {
            string message;
            bool ok = TryBuild(out message);
            EditorUtility.DisplayDialog("生成危字特效", message, "确定");
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
                message = "找不到「危.png」。请放到 Assets/Art/FX（或在 Project 里选中它）。";
                return false;
            }

            KanjiTexUtil.Prepare(tex);
            tex = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GetAssetPath(tex));

            Shader shader = Shader.Find("ARPG/FX/HealSprite");
            if (shader == null)
            {
                message = "找不到 Shader ARPG/FX/HealSprite。";
                return false;
            }

            Color tint = new Color(0.95f, 0.16f, 0.12f, 1f);
            Material coreMat = CreateMat(CoreMatPath, shader, tex, tint);

            GameObject root = new GameObject("PerilousWarning");
            PerilousWarningView view = root.AddComponent<PerilousWarningView>();
            MeshRenderer core = CreateQuad(root.transform, "Core", coreMat, CoreWidth);

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
                message = "保存 PerilousKanji.prefab 失败。";
                return false;
            }

            bool assigned = PlaceInScene(prefab);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            message = assigned
                ? "已用 危.png 生成 Billboard（与治/回生同一套画法），并挂到场景里。"
                : "已生成预制体，但没找到 CombatUIController。";
            return assigned;
        }

        static Texture2D FindKanjiTex()
        {
            return KanjiTexUtil.Find(
                new[] { TexFolder + "/危.png" },
                new[] { TexFolder, "Assets/Sekrio/FX", "Assets/Sekiro/FX", PrefabFolder },
                "危");
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
            instance.SetActive(true);
            Undo.RegisterCreatedObjectUndo(instance, "Place PerilousWarning");

            GameObject playerGo = GameObject.Find("Player");
            CharacterBody player = playerGo != null
                ? playerGo.GetComponent<CharacterBody>()
                : Object.FindObjectOfType<CharacterBody>(true);
            PerilousWarningView view = instance.GetComponent<PerilousWarningView>();
            if (view != null && player != null)
                view.BindFollowTarget(player);

            CombatUIController controller = Object.FindObjectOfType<CombatUIController>(true);
            if (controller != null)
            {
                Undo.RecordObject(controller, "Assign perilous warning");
                SerializedObject so = new SerializedObject(controller);
                SerializedProperty prop = so.FindProperty("perilousWarningView");
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

}
