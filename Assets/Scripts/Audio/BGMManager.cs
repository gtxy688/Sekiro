using DG.Tweening;
using UnityEngine;

using ARPG.FrameWork.Body;
using ARPG.Mgr;
namespace ARPG.Audio
{

    // 循环播放背景音乐，并在 Boss 剩最后一条命时切换可选的二阶段曲目。
    public class BGMManager : MonoBehaviour
    {
        [Header("背景音乐")]
        public AudioClip bgm;            // 默认战斗 BGM（循环）
        public AudioClip phase2Bgm;      // Boss 第二条命 BGM（可选，为空则不切）

        [Header("播放参数")]
        [Range(0.01f, 1f)]
        [Tooltip("暂停菜单音乐 100% 时的基础响度。曲子本身很大就调低这里，不要靠滑条贴 1%。")]
        public float volume = 0.1f;
        public float fadeDuration = 1f;  // 切换时的淡入淡出时长

        private AudioSource source;
        private AudioClip currentClip;
        private Tween fadeTween;
        private float fadeWeight;

        private void Awake()
        {
            // 使用独立 AudioSource，避免与同物体的音效播放器争用。
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            AudioVolumeSettings.Load();
        }

        private void Start()
        {
            PlayBGM(bgm);
        }

        private void OnEnable()
        {
            AudioVolumeSettings.OnChanged += ApplyOutputVolume;
            CombatEventBus.OnLifeCleared += HandleLifeCleared;
        }

        private void OnDisable()
        {
            AudioVolumeSettings.OnChanged -= ApplyOutputVolume;
            CombatEventBus.OnLifeCleared -= HandleLifeCleared;
        }

        // ===== 事件处理 =====

        // Boss 被处决一条命 → 剩最后一条命时切二阶段 BGM
        private void HandleLifeCleared(CharacterBody c, int remainingLives)
        {
            if (remainingLives == 1 && phase2Bgm != null)
            {
                PlayBGM(phase2Bgm);
            }
        }

        // ===== 播放控制 =====

        // 切换 BGM（淡出 → 换曲 → 淡入）；同一首直接忽略
        public void PlayBGM(AudioClip clip)
        {
            if (clip == null || clip == currentClip) return;

            fadeTween?.Kill();

            // 首播：直接淡入
            if (source.clip == null)
            {
                source.clip = clip;
                currentClip = clip;
                fadeWeight = 0f;
                ApplyOutputVolume();
                source.Play();
                fadeTween = FadeWeight(1f, fadeDuration);
                return;
            }

            // 换曲：淡出到 0 → 换 clip → 淡入
            fadeTween = FadeWeight(0f, fadeDuration * 0.5f)
                .OnComplete(() =>
                {
                    source.clip = clip;
                    currentClip = clip;
                    source.Play();
                    fadeTween = FadeWeight(1f, fadeDuration * 0.5f);
                });
        }

        // 淡出停止
        public void StopBGM()
        {
            fadeTween?.Kill();
            fadeTween = FadeWeight(0f, fadeDuration)
                .OnComplete(() =>
                {
                    source.Stop();
                    currentClip = null;
                });
        }

        // 只插值淡入淡出权重，再叠加用户音量，避免两者互相覆盖。
        private Tween FadeWeight(float target, float duration)
        {
            return DOTween.To(() => fadeWeight, w =>
            {
                fadeWeight = w;
                ApplyOutputVolume();
            }, target, duration).SetEase(Ease.Linear);
        }

        private void ApplyOutputVolume()
        {
            source.volume = volume * AudioVolumeSettings.Bgm * fadeWeight;
        }
    }

}
