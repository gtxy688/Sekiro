using System;
using UnityEngine;

namespace ARPG.Audio
{

    // 持久化音乐和音效两档用户音量，并通知播放器更新输出响度。
    public static class AudioVolumeSettings
    {
        public const string BgmKey = "audio.bgm";
        public const string SfxKey = "audio.sfx";
        public const float DefaultBgm = 0.8f;
        public const float DefaultSfx = 1f;

        public static event Action OnChanged;

        public static float Bgm { get; private set; } = DefaultBgm;
        public static float Sfx { get; private set; } = DefaultSfx;

        private static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            Bgm = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey, DefaultBgm));
            Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx));
            loaded = true;
        }

        public static void SetBgm(float value)
        {
            Load();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(Bgm, value)) return;
            Bgm = value;
            PlayerPrefs.SetFloat(BgmKey, Bgm);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void SetSfx(float value)
        {
            Load();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(Sfx, value)) return;
            Sfx = value;
            PlayerPrefs.SetFloat(SfxKey, Sfx);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }
    }

}
