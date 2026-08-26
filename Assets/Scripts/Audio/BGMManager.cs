using DG.Tweening;
using UnityEngine;

// 背景音乐播放器：循环播放 BGM，支持 Boss 第二条命 / 胜利时切换（淡入淡出）
// 订阅 CombatEventBus（表现层事件驱动，不轮询）。挂一个带 AudioSource 的空物体。
public class BGMManager : MonoBehaviour
{
    [Header("背景音乐")]
    public AudioClip bgm;            // 默认战斗 BGM（循环）
    public AudioClip phase2Bgm;      // Boss 第二条命 BGM（可选，为空则不切）

    [Header("播放参数")]
    [Range(0.01f, 1f)]
    [Tooltip("暂停菜单音乐 100% 时的基础响度。曲子本身很大就调低这里，不要靠滑条贴 1%。")]
    public float volume = 0.14f;
    public float fadeDuration = 1f;  // 切换时的淡入淡出时长

    private AudioSource source;
    private AudioClip currentClip;
    private Tween fadeTween;
    private float fadeWeight;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = true;
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
        CombatEventBus.OnVictory += HandleVictory;
    }

    private void OnDisable()
    {
        AudioVolumeSettings.OnChanged -= ApplyOutputVolume;
        CombatEventBus.OnLifeCleared -= HandleLifeCleared;
        CombatEventBus.OnVictory -= HandleVictory;
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

    private void HandleVictory(CharacterBody c)
    {
        if (victoryBgm != null)
        {
            PlayBGM(victoryBgm);
        }
        else
        {
            StopBGM();
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

    // 只 tween 淡入淡出权重，用户音量随时可改、不和淡入抢绝对值
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
        if (source == null) return;
        source.volume = volume * AudioVolumeSettings.Bgm * fadeWeight;
    }
}
