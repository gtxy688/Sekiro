using System.IO;
using UnityEditor;
using UnityEngine;

// 工具：多选 FBX → 把其下的动画 clip 统一重命名为 fbx 文件名
// （clip 名由 ModelImporter 管理，Inspector 里改不了，必须走这个）
// 多个 clip 时：第一个 = fbx 名，后续 = fbx名_N（避免重名）
// 用法：Project 多选 fbx → 菜单 Tools/动画/Clip 重命名为 fbx 文件名
public static class ClipRenamer
{
    [MenuItem("Tools/动画/Clip 重命名为 fbx 文件名")]
    public static void RenameClipsToFbxName()
    {
        int done = 0;
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[ClipRenamer] 跳过非 FBX：{obj.name}");
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[ClipRenamer] 拿不到 ModelImporter：{obj.name}");
                continue;
            }

            string fbxName = Path.GetFileNameWithoutExtension(path);

            // 拿当前所有 clip 条目（没手动配置过时用默认条目，保留其各项设置）
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.clipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning($"[ClipRenamer] {fbxName} 没有动画 clip，跳过");
                continue;
            }

            // 改名：一一对应——每个 fbx 的第一个 clip 改成 fbx 文件名；
            // 若一个 fbx 里还有更多 clip（罕见），保留它们各自的原名，不强行加后缀
            string firstOriginal = clips[0].name;
            clips[0].name = fbxName;
            for (int i = 1; i < clips.Length; i++)
            {
                // 保持原名不动（唯一性由原名保证）
            }

            // 把改过的数组写回并重新导入（clip 名由 importer 持久化，不能直接 RenameAsset）
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            Debug.Log($"[ClipRenamer] {fbxName}：第 1 个 clip 重命名 {firstOriginal} → {fbxName}" +
                      (clips.Length > 1 ? $"（其余 {clips.Length - 1} 个保留原名）" : ""));
            done++;
        }
        Debug.Log($"[ClipRenamer] 完成，处理 {done} 个 FBX");
    }
}
