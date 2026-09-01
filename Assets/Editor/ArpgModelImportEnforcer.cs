using System;

using UnityEditor;
using UnityEngine;
namespace ARPG.Editor
{
    // 新导入的动画 FBX 自动套用 Root Transform 规范，省掉每次手动跑 BatchRootTransformSettings。
    //
    // 两条自保规则：
    // 1. 只管 Assets/Sekrio/ 下的动画 FBX，别的一概不碰。
    // 2. 只在首次导入时接管（importSettingsMissing）。已经手工调过的配置绝不覆盖——
    //    导入期静默改写用户调整过的设置，是最招人恨的一类工具行为。
    //    覆盖时一律打 Log，让"为什么变了"有据可查。
    public class ArpgModelImportEnforcer : AssetPostprocessor
    {
        const string AnimRoot = "Assets/Sekrio/";

        void OnPreprocessModel()
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            if (!assetPath.StartsWith(AnimRoot, StringComparison.OrdinalIgnoreCase)) return;
            if (!assetPath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)) return;

            ModelImporter importer = assetImporter as ModelImporter;
            if (importer == null) return;

            if (!importer.importSettingsMissing) return;

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;

            int applied = 0;
            for (int i = 0; i < clips.Length; i++)
            {
                ModelImporterClipAnimation clip = clips[i];
                if (clip == null) continue;
                ApplyRootTransform(clip);
                applied++;
            }

            if (applied == 0) return;

            importer.clipAnimations = clips;
            Debug.Log($"[导入规范] {assetPath}：首次导入，已自动套用 Root Transform（{applied} 个片段）。" +
                      "手工改过之后不会再被自动覆盖。");
        }

        // 与 Tools/动画/批量设置 Root Transform 保持同一套数值
        static void ApplyRootTransform(ModelImporterClipAnimation clip)
        {
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.rotationOffset = 0f;

            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.heightFromFeet = false;
            clip.heightOffset = 0f;

            clip.lockRootPositionXZ = false;
            clip.keepOriginalPositionXZ = false;
        }
    }

}
