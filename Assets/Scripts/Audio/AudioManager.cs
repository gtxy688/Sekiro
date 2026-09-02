using UnityEngine;

using ARPG.Boss;
using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.Mgr;
using ARPG.Player;
namespace ARPG.Audio
{

    // 音效管理器：订阅 CombatEventBus，播放对应 AudioClip
    // 与 FXManager 同模式（事件驱动，不做每帧轮询）
    // 战斗音效从 Resources 文件夹装池，每次随机且不连抽同一条
    public class AudioManager : MonoBehaviour
    {
        private const string BlockFolder = "Sounds/Block";
        private const string DeflectFolder = "Sounds/Deflect";
        private const string PlayerHitFolder = "Sounds/Player/受击";
        private const string PlayerSwingFolder = "Sounds/Player/挥剑";
        private const string BossHitFolder = "Sounds/Boss/受击";
        private const string BossSwingFolder = "Sounds/Boss/挥剑";
        private const string BossArrowFolder = "Sounds/Boss/箭矢";
        private const string BossKickFolder = "Sounds/Boss/拳脚";
        private const string BossThrustFolder = "Sounds/Boss/t";
        private const string FinisherFolder = "Sounds/Finisher";
        private const string DeathFolder = "Sounds/Death";
        private const string GourdFolder = "Sounds/Gourd";
        private const string ReviveFolder = "Sounds/Revive";
        private const string VictoryFolder = "Sounds/Victory";

        [Header("播放参数")]
        [Range(0.01f, 1f)]
        [Tooltip("全局基础响度，× 暂停菜单音效滑条。wav 本身很大时优先调低这里。")]
        public float volume = 0.1f;

        [Header("分类响度（× volume × 音效滑条）")]
        [Range(0f, 2f)]
        [Tooltip("开刀 / 挥剑")]
        public float swingVolume = 1f;

        [Range(0f, 2f)]
        [Tooltip("受击")]
        public float hitVolume = 1f;

        private AudioSource audioSource;
        private AudioClip[] blockPool = System.Array.Empty<AudioClip>();
        private AudioClip[] deflectPool = System.Array.Empty<AudioClip>();
        private AudioClip[] playerHitPool = System.Array.Empty<AudioClip>();
        private AudioClip[] playerSwingPool = System.Array.Empty<AudioClip>();
        private AudioClip[] bossHitPool = System.Array.Empty<AudioClip>();
        private AudioClip[] bossSwingPool = System.Array.Empty<AudioClip>();
        private AudioClip[] bossArrowPool = System.Array.Empty<AudioClip>();
        private AudioClip[] bossKickPool = System.Array.Empty<AudioClip>();
        private AudioClip[] bossThrustPool = System.Array.Empty<AudioClip>();
        private AudioClip[] finisherPool = System.Array.Empty<AudioClip>();
        private AudioClip[] deathPool = System.Array.Empty<AudioClip>();
        private AudioClip[] gourdPool = System.Array.Empty<AudioClip>();
        private AudioClip[] revivePool = System.Array.Empty<AudioClip>();
        private AudioClip[] victoryPool = System.Array.Empty<AudioClip>();

        private int lastBlockIndex = -1;
        private int lastDeflectIndex = -1;
        private int lastPlayerHitIndex = -1;
        private int lastPlayerSwingIndex = -1;
        private int lastBossHitIndex = -1;
        private int lastBossSwingIndex = -1;
        private int lastBossArrowIndex = -1;
        private int lastBossKickIndex = -1;
        private int lastBossThrustIndex = -1;
        private int lastFinisherIndex = -1;
        private int lastDeathIndex = -1;
        private int lastGourdIndex = -1;
        private int lastReviveIndex = -1;
        private int lastVictoryIndex = -1;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;

            blockPool = LoadPool(BlockFolder);
            deflectPool = LoadPool(DeflectFolder);
            playerHitPool = LoadPool(PlayerHitFolder);
            playerSwingPool = LoadPool(PlayerSwingFolder);
            bossHitPool = LoadPool(BossHitFolder);
            bossSwingPool = LoadPool(BossSwingFolder);
            bossArrowPool = LoadPool(BossArrowFolder);
            bossKickPool = LoadPool(BossKickFolder);
            bossThrustPool = LoadPool(BossThrustFolder);
            finisherPool = LoadPool(FinisherFolder);
            deathPool = LoadPool(DeathFolder);
            gourdPool = LoadPool(GourdFolder);
            revivePool = LoadPool(ReviveFolder);
            victoryPool = LoadPool(VictoryFolder);

            AudioVolumeSettings.Load();
            ApplySfxVolume();
        }

        private void OnEnable()
        {
            AudioVolumeSettings.OnChanged += ApplySfxVolume;
            CombatEventBus.OnWeaponDeflected += HandleWeaponDeflected;
            CombatEventBus.OnTakeDamage += HandleTakeDamage;
            CombatEventBus.OnFinisherTriggered += HandleFinisherTriggered;
            CombatEventBus.OnDeath += HandleDeath;
            CombatEventBus.OnGourdUsed += HandleGourdUsed;
            CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
            CombatEventBus.OnVictory += HandleVictory;
            CombatEventBus.OnAttackSfx += HandleAttackSfx;
            CombatEventBus.OnAttackSwingStart += HandleAttackSwingStart;
            CombatEventBus.OnArrowReleased += HandleArrowReleased;
        }

        private void OnDisable()
        {
            AudioVolumeSettings.OnChanged -= ApplySfxVolume;
            CombatEventBus.OnWeaponDeflected -= HandleWeaponDeflected;
            CombatEventBus.OnTakeDamage -= HandleTakeDamage;
            CombatEventBus.OnFinisherTriggered -= HandleFinisherTriggered;
            CombatEventBus.OnDeath -= HandleDeath;
            CombatEventBus.OnGourdUsed -= HandleGourdUsed;
            CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
            CombatEventBus.OnVictory -= HandleVictory;
            CombatEventBus.OnAttackSfx -= HandleAttackSfx;
            CombatEventBus.OnAttackSwingStart -= HandleAttackSwingStart;
            CombatEventBus.OnArrowReleased -= HandleArrowReleased;
        }

        private void ApplySfxVolume()
        {
            if (audioSource == null) return;
            audioSource.volume = volume * AudioVolumeSettings.Sfx;
        }

        private static AudioClip[] LoadPool(string folder)
        {
            AudioClip[] loaded = Resources.LoadAll<AudioClip>(folder);
            if (loaded == null || loaded.Length == 0)
                return System.Array.Empty<AudioClip>();

            int n = 0;
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null) n++;
            }
            if (n == loaded.Length) return loaded;

            AudioClip[] filtered = new AudioClip[n];
            int w = 0;
            for (int i = 0; i < loaded.Length; i++)
            {
                if (loaded[i] != null) filtered[w++] = loaded[i];
            }
            return filtered;
        }

        private void PlayPool(
            AudioClip[] pool,
            ref int lastIndex,
            string folder,
            float volumeScale = 1f,
            bool warnIfEmpty = true)
        {
            if (pool == null || pool.Length == 0)
            {
                if (warnIfEmpty && !string.IsNullOrEmpty(folder))
                    Debug.LogWarning($"AudioManager: 音效池为空（Resources/{folder}）");
                return;
            }

            AudioClip clip = Pick(pool, ref lastIndex);
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, volumeScale);
        }

        private static AudioClip Pick(AudioClip[] pool, ref int lastIndex)
        {
            if (pool == null || pool.Length == 0) return null;

            int i;
            if (pool.Length == 1 || lastIndex < 0)
            {
                i = Random.Range(0, pool.Length);
            }
            else
            {
                i = Random.Range(0, pool.Length - 1);
                if (i >= lastIndex) i++;
            }

            lastIndex = i;
            return pool[i];
        }

        // 原本这里有一份自己的实现（查 PlayerBrain 组件），与 HitReactionUtil 那套
        // （比 PlayerRef 引用）标准不同，两者可能给出矛盾答案。已统一到阵营字段。
        private static bool IsPlayer(CharacterBody body) => HitReactionUtil.IsPlayer(body);

        private void HandleWeaponDeflected(Vector3 hitPoint, DeflectType type)
        {
            if (type == DeflectType.Perfect)
                PlayPool(deflectPool, ref lastDeflectIndex, DeflectFolder);
            else if (type == DeflectType.Normal)
                PlayPool(blockPool, ref lastBlockIndex, BlockFolder);
        }

        private void HandleTakeDamage(CharacterBody victim, int dmg, int currentHp)
        {
            if (victim == null) return;
            if (IsPlayer(victim))
                PlayPool(playerHitPool, ref lastPlayerHitIndex, PlayerHitFolder, hitVolume);
            else
                PlayPool(bossHitPool, ref lastBossHitIndex, BossHitFolder, hitVolume);
        }

        private void HandleAttackSwingStart(CharacterBody attacker)
        {
            if (attacker == null) return;

            if (IsPlayer(attacker))
            {
                PlayPool(playerSwingPool, ref lastPlayerSwingIndex, PlayerSwingFolder, swingVolume);
                return;
            }

            AttackConfig atk = attacker.ActiveAttack;
            BossMoveEntry entry = attacker.CurrentMoveEntry;
            if (IsKickOrPunch(entry, atk))
                PlayPool(bossKickPool, ref lastBossKickIndex, BossKickFolder, swingVolume);
            else if (IsThrust(entry, atk))
                PlayPool(bossThrustPool, ref lastBossThrustIndex, BossThrustFolder, swingVolume);
            else
                PlayPool(bossSwingPool, ref lastBossSwingIndex, BossSwingFolder, swingVolume);
        }

        private void HandleArrowReleased(CharacterBody shooter)
        {
            if (shooter == null || IsPlayer(shooter)) return;
            PlayPool(bossArrowPool, ref lastBossArrowIndex, BossArrowFolder, swingVolume);
        }

        // 拳脚：Kick 段、Elbow 投技判定槽
        private static bool IsKickOrPunch(BossMoveEntry entry, AttackConfig atk)
        {
            if (atk != null)
            {
                if (atk.HitboxSlot == AttackHitboxSlot.Elbow
                    || atk.HitboxSlot == AttackHitboxSlot.Kick)
                    return true;
                string anim = atk.AnimName;
                if (!string.IsNullOrEmpty(anim)
                    && (anim == "Kick" || anim == "Elbow"))
                    return true;
            }
            return entry != null && entry.id == "Kick";
        }

        // 突刺危 / 突刺动画走 t 库；普通横斩走挥剑库
        private static bool IsThrust(BossMoveEntry entry, AttackConfig atk)
        {
            if (atk != null)
            {
                if (atk.Perilous == PerilousType.Thrust) return true;
                string anim = atk.AnimName;
                if (!string.IsNullOrEmpty(anim)
                    && (anim == "Kengeki_Thrust"
                        || anim == "Jump_Danger"
                        || anim.IndexOf("Thrust", System.StringComparison.OrdinalIgnoreCase) >= 0))
                    return true;
            }
            return entry != null && entry.perilous == PerilousType.Thrust;
        }

        private void HandleFinisherTriggered(Vector3 pos)
        {
            PlayPool(finisherPool, ref lastFinisherIndex, FinisherFolder, warnIfEmpty: false);
        }

        private void HandleDeath(CharacterBody c)
        {
            PlayPool(deathPool, ref lastDeathIndex, DeathFolder, warnIfEmpty: false);
        }

        private void HandleGourdUsed(CharacterBody c, int remaining)
        {
            PlayPool(gourdPool, ref lastGourdIndex, GourdFolder, warnIfEmpty: false);
        }

        private void HandleReviveAvailable(CharacterBody c)
        {
            PlayPool(revivePool, ref lastReviveIndex, ReviveFolder, warnIfEmpty: false);
        }

        private void HandleVictory(CharacterBody c)
        {
            PlayPool(victoryPool, ref lastVictoryIndex, VictoryFolder, warnIfEmpty: false);
        }

        // 时间轴手动插的 sfxCues 仍走这里；与开刀池可并存
        private void HandleAttackSfx(AudioClip clip, Vector3 worldPos)
        {
            if (clip == null || audioSource == null) return;
            audioSource.PlayOneShot(clip, swingVolume);
        }
    }

}
