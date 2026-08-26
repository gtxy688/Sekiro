using UnityEngine;

// 音效管理器：订阅 CombatEventBus，播放对应 AudioClip
// 与 FXManager 同模式（事件驱动，不做每帧轮询）
// 格挡/弹反从 Resources 文件夹装池，每次随机且不连抽同一条
public class AudioManager : MonoBehaviour
{
    private const string BlockFolder = "Sounds/Block";
    private const string DeflectFolder = "Sounds/Deflect";

    [Header("音效资源")]
    public AudioClip playerHitSfx;    // 玩家受击
    public AudioClip bossHitSfx;      // Boss 受击
    public AudioClip perilousSfx;     // 危字警示
    public AudioClip finisherSfx;     // 忍杀处决
    public AudioClip deathSfx;        // 玩家死亡
    public AudioClip gourdSfx;        // 喝葫芦
    public AudioClip reviveSfx;       // 回生（M14）
    public AudioClip victorySfx;      // 胜利（M10）

    private AudioSource audioSource;
    private AudioClip[] blockPool = System.Array.Empty<AudioClip>();
    private AudioClip[] deflectPool = System.Array.Empty<AudioClip>();
    private int lastBlockIndex = -1;
    private int lastDeflectIndex = -1;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // 若没有 AudioSource，自动补一个（方便场景搭建）
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        blockPool = LoadPool(BlockFolder);
        deflectPool = LoadPool(DeflectFolder);

        AudioVolumeSettings.Load();
        ApplySfxVolume();
    }

    // 订阅事件
    private void OnEnable()
    {
        AudioVolumeSettings.OnChanged += ApplySfxVolume;
        CombatEventBus.OnWeaponDeflected += HandleWeaponDeflected;
        CombatEventBus.OnTakeDamage += HandleTakeDamage;
        CombatEventBus.OnPerilousAttack += HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered += HandleFinisherTriggered;
        CombatEventBus.OnDeath += HandleDeath;
        CombatEventBus.OnGourdUsed += HandleGourdUsed;
        CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
        CombatEventBus.OnVictory += HandleVictory;
        CombatEventBus.OnAttackSfx += HandleAttackSfx;
    }

    // 取消订阅
    private void OnDisable()
    {
        AudioVolumeSettings.OnChanged -= ApplySfxVolume;
        CombatEventBus.OnWeaponDeflected -= HandleWeaponDeflected;
        CombatEventBus.OnTakeDamage -= HandleTakeDamage;
        CombatEventBus.OnPerilousAttack -= HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered -= HandleFinisherTriggered;
        CombatEventBus.OnDeath -= HandleDeath;
        CombatEventBus.OnGourdUsed -= HandleGourdUsed;
        CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
        CombatEventBus.OnVictory -= HandleVictory;
        CombatEventBus.OnAttackSfx -= HandleAttackSfx;
    }

    private void ApplySfxVolume()
    {
        if (audioSource == null) return;
        audioSource.volume = AudioVolumeSettings.Sfx;
    }

    // ===== 资源池 =====

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

    // 池空返回 null 并 Warning；长 1 播那条；长 ≥ 2 均匀随机且 ≠ lastIndex。
    // 第一次 lastIndex < 0，在全池抽。
    private static AudioClip Pick(AudioClip[] pool, ref int lastIndex, string folder)
    {
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"AudioManager: 音效池为空（Resources/{folder}）");
            return null;
        }

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

    // ===== 事件处理 =====

    private void HandleWeaponDeflected(Vector3 hitPoint, DeflectType type)
    {
        AudioClip clip = null;
        if (type == DeflectType.Perfect)
            clip = Pick(deflectPool, ref lastDeflectIndex, DeflectFolder);
        else if (type == DeflectType.Normal)
            clip = Pick(blockPool, ref lastBlockIndex, BlockFolder);

        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    private void HandleTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        AudioClip clip = victim != null && victim.GetComponent<PlayerBrain>() != null
            ? playerHitSfx
            : bossHitSfx;
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    private void HandlePerilousAttack(PerilousType type)
    {
        if (perilousSfx != null)
        {
            audioSource.PlayOneShot(perilousSfx);
        }
    }

    private void HandleFinisherTriggered(Vector3 pos)
    {
        if (finisherSfx != null)
        {
            audioSource.PlayOneShot(finisherSfx);
        }
    }

    private void HandleDeath(CharacterBody c)
    {
        if (deathSfx != null)
        {
            audioSource.PlayOneShot(deathSfx);
        }
    }

    private void HandleGourdUsed(CharacterBody c, int remaining)
    {
        if (gourdSfx != null)
        {
            audioSource.PlayOneShot(gourdSfx);
        }
    }

    private void HandleReviveAvailable(CharacterBody c)
    {
        if (reviveSfx != null)
        {
            audioSource.PlayOneShot(reviveSfx);
        }
    }

    private void HandleVictory(CharacterBody c)
    {
        if (victorySfx != null)
        {
            audioSource.PlayOneShot(victorySfx);
        }
    }

    // 出招音：clip 由事件携带；第一版忽略 worldPos
    private void HandleAttackSfx(AudioClip clip, Vector3 worldPos)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
