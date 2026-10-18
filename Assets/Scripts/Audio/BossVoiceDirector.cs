using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using ARPG.Boss;
using ARPG.FrameWork.Body;
using ARPG.Mgr;
using ARPG.Player;
using ARPG.UI;
namespace ARPG.Audio
{

    // 按战斗事件播放弦一郎语音和字幕，开场台词结束前通过 IsOpeningHold 暂缓 Boss 行动。
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

        // 优先使用绑定的角色，尚未绑定时由 Start 查找场景角色。
        [SerializeField] private CharacterBody playerBody;
        [SerializeField] private CharacterBody bossBody;
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

        // 绑定战斗角色和字幕定位参照，由 CombatUIController 初始化或流程层更新。
        public void Bind(CharacterBody player, CharacterBody boss, RectTransform playerPosture)
        {
            playerBody = player;
            bossBody = boss;
            if (playerPosture != null)
                playerPostureRect = playerPosture;
            PlaceAbovePlayerPosture();
        }

        // 停止上一场台词并重置倒地台词标记，保留一次性的开场等待状态。
        // 复战不会自动重播开场词，因此不能在此重新锁住 Boss。
        public void ResetForEncounter()
        {
            if (playing != null)
            {
                StopCoroutine(playing);
                playing = null;
            }
            if (source != null)
                source.Stop();

            // 新一场允许再次播放倒地台词。
            playerDownLinePlayed = false;
            view?.ResetForEncounter();
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
            view.OnViewInit();
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
            if (playing != null)
                StopCoroutine(playing);
            source.Stop();
            playing = StartCoroutine(PlayRoutine(cues));
        }

        // 依次显示字幕并等待语音结束，整段演出完成后解除开场等待。
        private IEnumerator PlayRoutine(Cue[] cues)
        {
            EnsureView();
            ApplyVolume();

            for (int i = 0; i < cues.Length; i++)
            {
                Cue cue = cues[i];
                view.ShowLine(cue.text);

                AudioClip clip = LoadClip(cue.id);
                if (clip != null)
                {
                    source.clip = clip;
                    source.Play();
                    while (source.isPlaying)
                        yield return null;
                }
                else
                {
                    // 缺少可选语音时仍为字幕保留阅读时间。
                    yield return new WaitForSecondsRealtime(MissingClipFallback);
                }
            }

            yield return new WaitForSecondsRealtime(LingerAfterClip);
            view.HideLine();
            playing = null;
            IsOpeningHold = false;
        }

        // 缓存语音加载结果，避免同一资源重复加载或反复警告。
        private AudioClip LoadClip(string id)
        {
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

        // 以玩家架势条上边缘定位字幕，并转换到字幕父节点的局部坐标。
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
                playerBody = player.GetComponent<CharacterBody>();
            }

            if (bossBody == null)
            {
                BTBrain boss = FindObjectOfType<BTBrain>();
                bossBody = boss.GetComponent<CharacterBody>();
            }
        }

        private void ApplyVolume()
        {
            source.volume = volume * AudioVolumeSettings.Sfx;
        }
    }

}
