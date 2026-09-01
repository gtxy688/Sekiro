using UnityEditor;
using UnityEngine;

namespace ARPG.Editor
{
    // 治 / 危 / 回生 三张汉字贴图的查找与导入处理，三个 KanjiBuilder 共用。
    //
    // 为什么抽出来而不是各写一份：isReadable 必须在读完像素之后还原，否则贴图会常驻一份
    // CPU 可读副本（包体和运行时内存都翻倍）。这段"开了必须关"的逻辑散在三个文件里，
    // 改一处漏两处是迟早的事——所以收在这里，保证只有一条路径能碰它。
    public static class KanjiTexUtil
    {
        // 先在 preferredPaths 里精确找，找不到再去 folders 里按文件名包含 keyword 搜
        public static Texture2D Find(string[] preferredPaths, string[] folders, string keyword)
        {
            if (preferredPaths != null)
            {
                for (int i = 0; i < preferredPaths.Length; i++)
                {
                    if (string.IsNullOrEmpty(preferredPaths[i])) continue;
                    Texture2D hit = AssetDatabase.LoadAssetAtPath<Texture2D>(preferredPaths[i]);
                    if (hit != null) return hit;
                }
            }

            if (folders == null) return null;

            for (int f = 0; f < folders.Length; f++)
            {
                if (string.IsNullOrEmpty(folders[f])) continue;
                if (!AssetDatabase.IsValidFolder(folders[f])) continue;

                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folders[f] });
                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (!name.Contains(keyword)) continue;
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }

        // 汉字贴图的导入设置：亮度当字形用，不依赖 Alpha 通道。
        // 处理结束后 isReadable 一定是 false——想读像素请用 BakeLuminanceMask 那套临时开关。
        public static void Prepare(Texture2D tex)
        {
            if (tex == null) return;
            string path = AssetDatabase.GetAssetPath(tex);
            if (string.IsNullOrEmpty(path)) return;

            BakeLuminanceMask(path);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;

            // 字形贴图走压缩会糊边缘，这里保持不压缩；但可读标志必须关掉，两者无关
            importer.isReadable = false;
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

        // 有的汉字图是「白 RGB + Alpha 字形」，Shader 读亮度会变成实心方块。
        // 这里把亮度烘进 RGB，让 Shader 直接拿灰度当遮罩。
        //
        // isReadable 只在读像素这一小段临时打开，用完连同压缩设置一起还原。
        static void BakeLuminanceMask(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool wasReadable = importer.isReadable;
            TextureImporterCompression wasCompression = importer.textureCompression;

            importer.isReadable = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();

            bool baked = TryBakePixels(path);

            // 无论烤没烤成都还原：不能因为提前 return 就把可读标志留在那儿
            TextureImporter restore = AssetImporter.GetAtPath(path) as TextureImporter;
            if (restore != null)
            {
                restore.isReadable = wasReadable;
                restore.textureCompression = wasCompression;
                restore.SaveAndReimport();
            }

            if (baked)
                AssetDatabase.ImportAsset(path);
        }

        static bool TryBakePixels(string path)
        {
            Texture2D src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (src == null) return false;

            Color[] px;
            try
            {
                px = src.GetPixels();
            }
            catch (UnityException e)
            {
                Debug.LogWarning($"[KanjiTex] 读不到像素（贴图不可读？）：{path}\n{e.Message}");
                return false;
            }

            bool changed = false;
            for (int i = 0; i < px.Length; i++)
            {
                float lum = Mathf.Max(px[i].r, Mathf.Max(px[i].g, px[i].b)) * px[i].a;
                if (Mathf.Abs(px[i].r - lum) > 0.01f || px[i].a < 0.99f)
                    changed = true;
                px[i] = new Color(lum, lum, lum, 1f);
            }
            if (!changed) return false;

            Texture2D dst = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
            dst.SetPixels(px);
            dst.Apply();
            byte[] bytes = dst.EncodeToPNG();
            Object.DestroyImmediate(dst);

            System.IO.File.WriteAllBytes(path, bytes);
            return true;
        }
    }

}
