using DG.Tweening;
using UnityEngine;

// 背景音乐播放器：循环播放 BGM，支持 Boss 第二条命 / 胜利时切换（淡入淡出）
// 订阅 CombatEventBus（表现层事件驱动，不轮询）。挂一个带 AudioSource 的空物体。
public class BGMManager : MonoBehaviour
{
    [Header("背景音乐")]
    public AudioClip bgm;            // 默认战斗 BGM（循环）
    public AudioClip phase2Bgm;      // Boss 第二条命 BGM（可选，为空则不切）
    public AudioClip victoryBgm;     // 胜利 BGM（可选，为空则淡出停止）

    [Header("播放参数")]
    [Range(0f, 1f)] public float volume = 0.8f;
    public float fadeDuration = 1f;  // 切换时的淡入淡出时长

    private AudioSource source;
    private AudioClip currentClip;
    private Tween fadeTween;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
        }
        source.playOnAwake = false;
        source.loop = true;
    }

    private void Start()
    {
        PlayBGM(bgm);
    }

    private void OnEnable()
    {
        CombatEventBus.OnLifeCleared += HandleLifeCleared;
        CombatEventBus.OnVictory += HandleVictory;
    }

    private void OnDisable()
    {
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
            source.volume = 0f;
            source.Play();
            fadeTween = FadeVolume(volume, fadeDuration);
            return;
        }

        // 换曲：淡出到 0 → 换 clip → 淡入
        fadeTween = FadeVolume(0f, fadeDuration * 0.5f)
            .OnComplete(() =>
            {
                source.clip = clip;
                currentClip = clip;
                source.Play();
                fadeTween = FadeVolume(volume, fadeDuration * 0.5f);
            });
    }

    // 淡出停止
    public void StopBGM()
    {
        fadeTween?.Kill();
        fadeTween = FadeVolume(0f, fadeDuration)
            .OnComplete(() =>
            {
                source.Stop();
                currentClip = null;
            });
    }

    // 音量淡入淡出：用 DOTween 核心（DOTween.To）实现，不依赖 Audio 扩展模块（避免没启用 DOTweenModuleAudio 时报 DOFade 缺失）
    private Tween FadeVolume(float target, float duration)
    {
        return DOTween.To(() => source.volume, v => source.volume = v, target, duration)
            .SetEase(Ease.Linear);
    }
}
