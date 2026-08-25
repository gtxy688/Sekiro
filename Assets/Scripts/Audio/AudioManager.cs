using UnityEngine;

// 音效管理器：订阅 CombatEventBus，播放对应 AudioClip
// 与 FXManager 同模式（事件驱动，不做每帧轮询）
// 用法：场景里挂一个带 AudioSource 的 GameObject，拖入音效资源
public class AudioManager : MonoBehaviour
{
    [Header("音效资源")]
    public AudioClip deflectSfx;      // 完美弹反"叮"（清脆）
    public AudioClip blockSfx;        // 普通防御"笃"（沉闷）
    public AudioClip hitSfx;          // 受击
    public AudioClip perilousSfx;     // 危字警示
    public AudioClip finisherSfx;     // 忍杀处决
    public AudioClip deathSfx;        // 玩家死亡
    public AudioClip gourdSfx;        // 喝葫芦
    public AudioClip reviveSfx;       // 回生（M14）
    public AudioClip victorySfx;      // 胜利（M10）

    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // 若没有 AudioSource，自动补一个（方便场景搭建）
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
    }

    // 订阅事件
    private void OnEnable()
    {
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

    // ===== 事件处理 =====

    // 武器交锋：完美弹反 → "叮"，普通防御 → "笃"
    private void HandleWeaponDeflected(Vector3 hitPoint, DeflectType type)
    {
        if (type == DeflectType.Perfect && deflectSfx != null)
        {
            audioSource.PlayOneShot(deflectSfx);
        }
        else if (type == DeflectType.Normal && blockSfx != null)
        {
            audioSource.PlayOneShot(blockSfx);
        }
    }

    // 受击（这里只播 hitSfx；区分玩家/Boss 的音效可后续加字段）
    private void HandleTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        if (hitSfx != null)
        {
            audioSource.PlayOneShot(hitSfx);
        }
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
