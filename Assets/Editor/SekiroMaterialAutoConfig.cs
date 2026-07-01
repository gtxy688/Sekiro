using UnityEditor;
using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 只狼解包资源自动材质配置工具
/// 根据贴图命名规则自动匹配并配置材质
///
/// 使用方法：
///   1. 将脚本放在 Assets/Editor 目录下
///   2. Unity 菜单 → 工具 → 只狼资源 → 配置弦一郎材质
///   3. 自动完成所有材质→贴图的分配
/// </summary>
public class SekiroMaterialAutoConfig : EditorWindow
{
    private string resourceRoot = "Assets/Resources/只狼/苇名弦一郎";
    private bool logDetails = true;

    [MenuItem("工具/只狼资源/配置弦一郎材质")]
    public static void ShowWindow()
    {
        var window = GetWindow<SekiroMaterialAutoConfig>("弦一郎材质配置");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("只狼解包资源 — 弦一郎自动材质配置", EditorStyles.boldLabel);
        EditorGUILayout.Space(5);

        resourceRoot = EditorGUILayout.TextField("资源根目录", resourceRoot);
        logDetails = EditorGUILayout.Toggle("显示详细日志", logDetails);

        EditorGUILayout.Space(10);

        if (GUILayout.Button("开始自动配置", GUILayout.Height(40)))
        {
            RunAutoConfig();
        }

        EditorGUILayout.Space(5);

        if (GUILayout.Button("只重新分配材质到FBX槽位", GUILayout.Height(30)))
        {
            ReassignMaterialsToFBX();
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "贴图命名规则：\n" +
            "  _a  = Albedo (BaseMap)\n" +
            "  _m  = Metallic (MetallicMap)\n" +
            "  _n  = Normal (NormalMap)\n" +
            "  _em = Emission (EmissionMap)\n\n" +
            "材质命名 → 贴图前缀映射见代码中的 mapping 字典",
            MessageType.Info);
    }

    private void RunAutoConfig()
    {
        string texDir = Path.Combine(resourceRoot, "c7100-Tex");
        string matDir = Path.Combine(resourceRoot, "Material");

        if (!Directory.Exists(texDir))
        {
            EditorUtility.DisplayDialog("错误", $"找不到贴图目录：{texDir}", "确定");
            return;
        }
        if (!Directory.Exists(matDir))
        {
            EditorUtility.DisplayDialog("错误", $"找不到材质目录：{matDir}", "确定");
            return;
        }

        // 收集所有贴图资源
        var textures = new Dictionary<string, Texture2D>();
        string[] texFiles = Directory.GetFiles(texDir, "*.png");
        foreach (string file in texFiles)
        {
            string assetPath = FileUtil.GetProjectRelativePath(file);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (tex != null)
            {
                // 用文件名（无扩展名）作为 key
                string key = Path.GetFileNameWithoutExtension(file);
                textures[key] = tex;

                // 设置贴图属性
                ConfigureTextureImportSettings(assetPath, key);
            }
        }

        if (logDetails)
            Debug.Log($"[弦一郎材质配置] 找到 {textures.Count} 张贴图");

        // 材质 → 贴图前缀映射
        // 键：材质名（不含 .mat），值：贴图文件名前缀列表
        var mapping = new Dictionary<string, string[]>()
        {
            { "body", new string[] { "c7100_armor" } },
            { "face", new string[] { "c7109_Skin_A", "c7109_Head_Expression" } },
            { "hair", new string[] { "c7109_hair" } },
            { "hair_Top", new string[] { "c7109_hair" } },
            { "hand", new string[] { "c7109_Skin_B" } },
            { "eye", new string[] { "c7109_Eye" } },
            { "eyeGlass", new string[] { "c7109_Eye" } },
            { "hat", new string[] { "c7100_helmet" } },
            { "leg", new string[] { "c7100_parts" } },
            { "shose", new string[] { "c7100_parts" } },
            { "mouth", new string[] { "c7109_Skin_C" } },
            { "teeth", new string[] { "c7109_Skin_D" } },
            { "pifeng", new string[] { "c7100_manto" } },
            { "pifeng1", new string[] { "c7100_chainmail02" } },
            { "bow", new string[] { "c7109_we_bow01" } },
            { "arrow", new string[] { "c7109_we_arrow01" } },
            { "katana", new string[] { "c7109_we_sword01" } },
            { "slash", new string[] { "c7100_sword" } },
        };

        // 为每个材质分配贴图
        string[] matFiles = Directory.GetFiles(matDir, "*.mat");
        int configuredCount = 0;

        foreach (string matFile in matFiles)
        {
            string matName = Path.GetFileNameWithoutExtension(matFile);
            string matPath = FileUtil.GetProjectRelativePath(matFile);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            if (mat == null)
            {
                if (logDetails)
                    Debug.LogWarning($"[弦一郎材质配置] 无法加载材质：{matPath}");
                continue;
            }

            if (!mapping.ContainsKey(matName))
            {
                if (logDetails)
                    Debug.LogWarning($"[弦一郎材质配置] 材质 '{matName}' 未在映射表中，跳过");
                continue;
            }

            string[] prefixes = mapping[matName];
            bool found = false;

            foreach (string prefix in prefixes)
            {
                // 找 BaseMap (_a)
                if (textures.ContainsKey(prefix + "_a"))
                {
                    mat.SetTexture("_BaseMap", textures[prefix + "_a"]);
                    mat.SetTexture("_MainTex", textures[prefix + "_a"]);
                    found = true;

                    if (logDetails)
                        Debug.Log($"[弦一郎材质配置] {matName} → BaseMap: {prefix}_a");
                }

                // 找 NormalMap (_n)
                if (textures.ContainsKey(prefix + "_n"))
                {
                    mat.SetTexture("_BumpMap", textures[prefix + "_n"]);
                    mat.EnableKeyword("_NORMALMAP");
                    mat.SetFloat("_BumpScale", 1f);

                    if (logDetails)
                        Debug.Log($"[弦一郎材质配置] {matName} → NormalMap: {prefix}_n");
                }

                // 找 MetallicMap (_m)
                if (textures.ContainsKey(prefix + "_m"))
                {
                    mat.SetTexture("_MetallicGlossMap", textures[prefix + "_m"]);
                    mat.SetFloat("_Metallic", 1f);
                    mat.SetFloat("_Smoothness", 0.5f);

                    if (logDetails)
                        Debug.Log($"[弦一郎材质配置] {matName} → MetallicMap: {prefix}_m");
                }

                // 找 EmissionMap (_em)
                if (textures.ContainsKey(prefix + "_em"))
                {
                    mat.SetTexture("_EmissionMap", textures[prefix + "_em"]);
                    mat.SetColor("_EmissionColor", Color.white);

                    if (logDetails)
                        Debug.Log($"[弦一郎材质配置] {matName} → EmissionMap: {prefix}_em");
                }

                if (found) break; // 第一个匹配的前缀就够了
            }

            if (found)
            {
                EditorUtility.SetDirty(mat);
                configuredCount++;
            }
        }

        // 保存所有修改
        AssetDatabase.SaveAssets();

        Debug.Log($"[弦一郎材质配置] 完成！共配置 {configuredCount} 个材质");

        // 自动重新分配材质到 FBX 槽位
        ReassignMaterialsToFBX();

        EditorUtility.DisplayDialog("完成",
            $"已配置 {configuredCount} 个材质\n已重新分配到 FBX 槽位", "确定");
    }

    /// <summary>
    /// 配置贴图导入设置（确保 Normal Map 正确标记）
    /// </summary>
    private void ConfigureTextureImportSettings(string assetPath, string fileName)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;

        bool needsReimport = false;

        // _n 后缀的贴图标记为 Normal Map
        if (fileName.EndsWith("_n"))
        {
            if (importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                needsReimport = true;
            }
        }
        else
        {
            // 其他贴图确保是 Default 类型
            if (importer.textureType != TextureImporterType.Default)
            {
                importer.textureType = TextureImporterType.Default;
                needsReimport = true;
            }

            // _m 是金属度贴图，不需要 sRGB
            if (fileName.EndsWith("_m"))
            {
                if (importer.sRGBTexture != false)
                {
                    importer.sRGBTexture = false;
                    needsReimport = true;
                }
            }
            else if (fileName.EndsWith("_a"))
            {
                // _a 是 Albedo，需要 sRGB
                if (importer.sRGBTexture != true)
                {
                    importer.sRGBTexture = true;
                    needsReimport = true;
                }
            }
        }

        if (needsReimport)
        {
            AssetDatabase.ImportAsset(assetPath);
        }
    }

    /// <summary>
    /// 将提取的材质重新分配到 FBX 的材质槽位
    /// 使用 SerializedObject 方式，兼容 Unity 2022
    /// </summary>
    private void ReassignMaterialsToFBX()
    {
        // 查找 FBX 文件
        string fbxPath = null;
        string[] fbxFiles = Directory.GetFiles(resourceRoot, "*.fbx");
        foreach (string f in fbxFiles)
        {
            string relPath = FileUtil.GetProjectRelativePath(f);
            if (!relPath.Contains("OutFbxAnimation") && !relPath.Contains("FbxWithSkeleton") && !relPath.Contains("FbxSkeleton"))
            {
                fbxPath = relPath;
                break;
            }
        }

        if (fbxPath == null)
        {
            Debug.LogWarning("[弦一郎材质配置] 未找到主模型 FBX");
            return;
        }

        // 加载所有提取的材质
        string matDir = Path.Combine(resourceRoot, "Material");
        var extractedMats = new Dictionary<string, Material>();
        string[] matFiles = Directory.GetFiles(matDir, "*.mat");
        foreach (string mf in matFiles)
        {
            string name = Path.GetFileNameWithoutExtension(mf);
            string path = FileUtil.GetProjectRelativePath(mf);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                extractedMats[name] = mat;
            }
        }

        // 通过 SerializedObject 修改 FBX 的材质映射
        var fbxObj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(fbxPath);
        if (fbxObj == null)
        {
            Debug.LogWarning($"[弦一郎材质配置] 无法加载 FBX: {fbxPath}");
            return;
        }

        var so = new SerializedObject(fbxObj);
        var externalObjectsProp = so.FindProperty("m_ExternalObjects");

        if (externalObjectsProp == null)
        {
            // Fallback: 直接标记 dirty，让 Unity 自动刷新
            EditorUtility.SetDirty(fbxObj);
            AssetDatabase.SaveAssets();
            Debug.Log("[弦一郎材质配置] 使用 fallback 方式刷新 FBX");
            return;
        }

        int assignedCount = 0;
        for (int i = 0; i < externalObjectsProp.arraySize; i++)
        {
            var elem = externalObjectsProp.GetArrayElementAtIndex(i);
            var nameProp = elem.FindPropertyRelative("first.name");
            var valueProp = elem.FindPropertyRelative("second");

            if (nameProp != null && valueProp != null)
            {
                string slotName = nameProp.stringValue;
                if (extractedMats.ContainsKey(slotName))
                {
                    valueProp.objectReferenceValue = extractedMats[slotName];
                    assignedCount++;
                    if (logDetails)
                        Debug.Log($"[弦一郎材质配置] 槽位 '{slotName}' → {extractedMats[slotName].name}");
                }
            }
        }

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();

        Debug.Log($"[弦一郎材质配置] 已将 {assignedCount} 个材质分配到 FBX 槽位");
    }
}
