using System.IO;
using UnityEditor;
using UnityEngine;

// 工具：把选中 FBX 的每个动画 clip 生成一份"原地版" .anim 资源
// 原理：剔除 RootPos / RootRotXZ / RootRot 等根骨骼的动画曲线（骨骼平移/旋转），
//       保留挥刀/姿势曲线 → 角色视觉不再前后漂移，位移完全交给攻击动画 Root 曲线（全权根运动）
// 用法：Project 里选中攻击动画 fbx → 右键/菜单 Tools/动画/生成原地版动画
public static class InPlaceClipGenerator
{
    [MenuItem("Tools/动画/生成原地版动画（剔除根骨骼曲线）")]
    public static void GenerateInPlaceClips()
    {
        foreach (Object obj in Selection.objects)
        {
            string path = AssetDatabase.GetAssetPath(obj);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning($"[InPlace] 跳过非 FBX：{obj.name}");
                continue;
            }

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
            string fbxBaseName = Path.GetFileNameWithoutExtension(path);
            foreach (Object asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview"))
                {
                    CreateInPlaceClip(path, fbxBaseName, clip);
                }
            }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[InPlace] 生成完成");
    }

    private static void CreateInPlaceClip(string fbxPath, string fbxBaseName, AnimationClip source)
    {
        // 动画名统一 = fbx 文件名（如 a076_405010），方便管理；
        // Animator 状态名引用的是状态名，不依赖这里的 clip 名
        string clipName = fbxBaseName;

        AnimationClip dest = new AnimationClip();
        dest.frameRate = source.frameRate;
        dest.name = clipName;

        int copied = 0;
        int skipped = 0;

        // 复制除根骨骼外的所有曲线（含肌肉空间 RootT/RootQ 剔除）
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
        {
            if (IsRootBoneBinding(binding) || IsRootMuscleBinding(binding))
            {
                skipped++;
                continue;
            }
            AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);
            if (curve != null)
            {
                AnimationUtility.SetEditorCurve(dest, binding, curve);
                copied++;
            }
        }

        // 关键：.anim 的根运动由 clip 自身 settings 决定（不继承 fbx 导入设置）。
        // 强制三项全 Bake Into Pose → .anim 自身不产生任何根位移/旋转，纯姿势
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
        settings.keepOriginalOrientation = false;   // Root Transform Rotation → Bake
        settings.keepOriginalPositionY = false;     // Position Y → Bake
        settings.keepOriginalPositionXZ = false;    // Position XZ → Bake
        AnimationUtility.SetAnimationClipSettings(dest, settings);

        // 剔除的根骨骼名单（只狼骨架的根控制骨骼）
        string outPath = Path.Combine(Path.GetDirectoryName(fbxPath), $"{clipName}_inplace.anim");
        AssetDatabase.CreateAsset(dest, outPath);

        // 打印所有带曲线的骨骼名（去重），方便排查"还有哪个根骨骼没剔干净"
        var boneNames = new System.Collections.Generic.HashSet<string>();
        foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(source))
        {
            // 空路径 = 肌肉空间曲线，显示属性名（RootT/RootQ/Muscle）
            boneNames.Add(string.IsNullOrEmpty(b.path) ? $"[muscle]{b.propertyName}" : b.path);
        }
        Debug.Log($"[InPlace] {clipName} → 复制 {copied} 条曲线，剔除 {skipped} 条根骨骼曲线 → {outPath}");
        Debug.Log($"[InPlace] {clipName} 原始骨骼曲线清单：{string.Join(", ", boneNames)}");
    }

    // 根骨骼：RootPos（平移根）/ RootRotXZ / RootRot（旋转根）/ Master / Root / Root_ForUE（顶层根）
    private static bool IsRootBoneBinding(EditorCurveBinding binding)
    {
        if (binding.type != typeof(Transform)) return false;
        if (binding.path.Contains("RootPos")) return true;
        if (binding.path.Contains("RootRotXZ")) return true;
        if (binding.path.Contains("RootRot")) return true;
        if (binding.path.Contains("Root_ForUE")) return true;
        if (binding.path.Contains("Master")) return true;
        if (binding.path.Contains("Root")) return true;
        return false;
    }

    // 肌肉空间根曲线：RootT（身体平移，就是"前进/后退"的元凶）、RootQ（身体旋转，就是"转向"的元凶）
    // 剔除它们 = 角色视觉完全原地，挥刀肌肉曲线（MuscleX）全保留
    private static bool IsRootMuscleBinding(EditorCurveBinding binding)
    {
        if (binding.propertyName.StartsWith("RootT")) return true;
        if (binding.propertyName.StartsWith("RootQ")) return true;
        return false;
    }
}
