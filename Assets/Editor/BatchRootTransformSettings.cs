using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 多选单片段 FBX 后，仅统一 Root Transform 导入设置，保留其他动画参数。
public static class BatchRootTransformSettings
{
    private const string MenuPath = "Tools/动画/批量设置 Root Transform";

    [MenuItem(MenuPath)]
    public static void ApplyToSelectedFbx()
    {
        HashSet<string> processedPaths = new HashSet<string>();
        int successCount = 0;

        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            string assetPath = AssetDatabase.GetAssetPath(selectedObject);
            if (!IsFbx(assetPath) || !processedPaths.Add(assetPath))
            {
                continue;
            }

            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[RootTransform] 无法获取 ModelImporter，已跳过：{assetPath}");
                continue;
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length != 1)
            {
                int clipCount = clips == null ? 0 : clips.Length;
                Debug.LogWarning($"[RootTransform] 要求 FBX 仅含一个动画片段，已跳过：{assetPath}（片段数：{clipCount}）");
                continue;
            }

            ModelImporterClipAnimation clip = clips[0];

            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.rotationOffset = 0f;

            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.heightOffset = 0f;

            clip.lockRootPositionXZ = false;
            clip.keepOriginalPositionXZ = false;

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            successCount++;

            Debug.Log($"[RootTransform] 已设置：{assetPath}");
        }

        Debug.Log($"[RootTransform] 批量设置结束，成功处理 {successCount} 个 FBX");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateApplyToSelectedFbx()
    {
        foreach (UnityEngine.Object selectedObject in Selection.objects)
        {
            if (IsFbx(AssetDatabase.GetAssetPath(selectedObject)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsFbx(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath)
            && assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase);
    }
}
