using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 弦一郎语音 + 台词。时机走事件总线，不轮询战斗数值。
public class BossVoiceDirector : MonoBehaviour
{
    public static BossVoiceDirector Instance { get; private set; }

    public bool IsOpeningHold { get; private set; } = true;

    private const string VoiceFolder = "Voices";
    private const float MissingClipFallback = 2.4f;
    private const float LingerAfterClip = 0.35f;

    [Range(0.01f, 1f)]
    [Tooltip("暂停菜单音效 100% 时的语音基础响度。")]
    public float volume = 0.2f;

    private CharacterBody playerBody;
    private CharacterBody bossBody;
    private RectTransform playerPostureRect;
    private VoiceLineView view;
    private AudioSource source;
    private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
    private Coroutine playing;
    private bool playerDownLinePlayed;

    private struct Cue
    {
        public string id;
        public string text;

        public Cue(string id, string text)
        {
            this.id = id;
            this.text = text;
        }
    }

    public void Bind(CharacterBody player, CharacterBody boss, RectTransform playerPosture)
    {
        playerBody = player;
        bossBody = boss;
        if (playerPosture != null)
            playerPostureRect = playerPosture;
        PlaceAbovePlayerPosture();
    }

    private void Awake()
    {
        Instance = this;
        IsOpeningHold = true;
        source = GetComponent<AudioSource>();
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        AudioVolumeSettings.Load();
        ApplyVolume();
    }

    private void OnEnable()
    {
        AudioVolumeSettings.OnChanged += ApplyVolume;
        CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
        CombatEventBus.OnDeath += HandleDeath;
        CombatEventBus.OnRevived += HandleRevived;
        CombatEventBus.OnLifeCleared += HandleLifeCleared;
        CombatEventBus.OnVictory += HandleVictory;
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
        IsOpeningHold = false;
        AudioVolumeSettings.OnChanged -= ApplyVolume;
        CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
        CombatEventBus.OnDeath -= HandleDeath;
        CombatEventBus.OnRevived -= HandleRevived;
        CombatEventBus.OnLifeCleared -= HandleLifeCleared;
        CombatEventBus.OnVictory -= HandleVictory;
    }

    private void Start()
    {
        ResolveBodies();
        ResolvePlayerPosture();
        EnsureView();
        Canvas.ForceUpdateCanvases();
        PlaceAbovePlayerPosture();
        view?.OnViewInit();
        Play(new Cue("160000", "我上了"));
    }

    private void HandleReviveAvailable(CharacterBody c)
    {
        if (c != playerBody) return;
        playerDownLinePlayed = true;
        Play(new Cue("160400", "我，一定会守护苇名"));
    }

    private void HandleDeath(CharacterBody c)
    {
        if (c != playerBody) return;
        // 有回生时倒地已经播过，超时真死不要再念一遍
        if (playerDownLinePlayed) return;
        playerDownLinePlayed = true;
        Play(new Cue("160400", "我，一定会守护苇名"));
    }

    private void HandleRevived(CharacterBody c)
    {
        if (c != playerBody) return;
        playerDownLinePlayed = false;
        Play(
            new Cue("160200", "是龙胤的力量吗。"),
            new Cue("160201", "那么,无论多少次杀死你为止。"));
    }

    private void HandleLifeCleared(CharacterBody c, int remainingLives)
    {
        if (c != bossBody) return;
        if (remainingLives <= 0) return;
        Play(new Cue("160300", "还没完,神子的忍者!"));
    }

    private void HandleVictory(CharacterBody c)
    {
        if (c != bossBody) return;
        Play(new Cue("160500", "苇名。。"));
    }

    private void Play(params Cue[] cues)
    {
        if (cues == null || cues.Length == 0) return;
        if (playing != null)
            StopCoroutine(playing);
        if (source != null)
            source.Stop();
        playing = StartCoroutine(PlayRoutine(cues));
    }

    private IEnumerator PlayRoutine(Cue[] cues)
    {
        EnsureView();
        ApplyVolume();

        for (int i = 0; i < cues.Length; i++)
        {
            Cue cue = cues[i];
            view?.ShowLine(cue.text);

            AudioClip clip = LoadClip(cue.id);
            if (clip != null && source != null)
            {
                source.clip = clip;
                source.Play();
                while (source.isPlaying)
                    yield return null;
            }
            else
            {
                yield return new WaitForSecondsRealtime(MissingClipFallback);
            }
        }

        yield return new WaitForSecondsRealtime(LingerAfterClip);
        view?.HideLine();
        playing = null;
        IsOpeningHold = false;
    }

    private AudioClip LoadClip(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (clips.TryGetValue(id, out AudioClip cached))
            return cached;

        AudioClip loaded = Resources.Load<AudioClip>(VoiceFolder + "/" + id);
        clips[id] = loaded;
        if (loaded == null)
            Debug.LogWarning($"BossVoiceDirector: 找不到语音 Resources/{VoiceFolder}/{id}");
        return loaded;
    }

    private void EnsureView()
    {
        if (view != null) return;

        view = GetComponentInChildren<VoiceLineView>(true);
        if (view != null) return;

        GameObject go = new GameObject("VoiceLine", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(transform, false);
        view = go.AddComponent<VoiceLineView>();
        view.OnViewInit();
        PlaceAbovePlayerPosture();
    }

    private void ResolvePlayerPosture()
    {
        if (playerPostureRect != null) return;

        Transform named = transform.Find("PlayerPosture");
        if (named != null)
        {
            playerPostureRect = named as RectTransform;
            return;
        }

        BossPostureBarView[] bars = GetComponentsInChildren<BossPostureBarView>(true);
        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] != null && bars[i].name == "PlayerPosture")
            {
                playerPostureRect = bars[i].transform as RectTransform;
                return;
            }
        }
    }

    // 钉在底栏正中那条 PlayerPosture 上面，不要贴左下血条
    private void PlaceAbovePlayerPosture()
    {
        if (view == null) return;

        RectTransform rect = view.transform as RectTransform;
        RectTransform parent = rect.parent as RectTransform;
        if (rect == null || parent == null) return;

        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(720f, 48f);

        if (playerPostureRect == null)
        {
            rect.anchoredPosition = new Vector2(0f, 200f);
            return;
        }

        Vector3[] corners = new Vector3[4];
        playerPostureRect.GetWorldCorners(corners);
        Vector3 topCenter = (corners[1] + corners[2]) * 0.5f;
        Vector3 local = parent.InverseTransformPoint(topCenter);
        Vector2 anchorLocal = parent.rect.min + Vector2.Scale(parent.rect.size, rect.anchorMin);
        rect.anchoredPosition = new Vector2(local.x, local.y) - anchorLocal + new Vector2(0f, 8f);
    }

    private void ResolveBodies()
    {
        if (playerBody == null)
        {
            PlayerBrain player = FindObjectOfType<PlayerBrain>();
            if (player != null)
                playerBody = player.GetComponent<CharacterBody>();
        }

        if (bossBody == null)
        {
            BTBrain boss = FindObjectOfType<BTBrain>();
            if (boss != null)
                bossBody = boss.GetComponent<CharacterBody>();
        }
    }

    private void ApplyVolume()
    {
        if (source == null) return;
        source.volume = volume * AudioVolumeSettings.Sfx;
    }
}
