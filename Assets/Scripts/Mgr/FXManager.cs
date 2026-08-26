using UnityEngine;

public class FXManager : MonoBehaviour
{
    [Header("特效预制体")]
    public GameObject normalBlockSparks;
    public GameObject perfectParrySparks;
    [Tooltip("旧新月贴图，宽刀光带不再使用")]
    public GameObject playerSlashFx;
    public Material playerSwingTrailMat;

    [Header("玩家刀光颜色")]
    [Tooltip("旧字段，宽刀光带不再使用")]
    [ColorUsage(true, true)]
    public Color playerSlashColor = new Color(0.85f, 0.95f, 1f, 1f);
    [Tooltip("刀光带靠近刀刃（挥砍前缘）")]
    [ColorUsage(true, true)]
    public Color playerTrailStart = Color.white;
    [Tooltip("刀光带远离刀刃的弧尾")]
    [ColorUsage(true, true)]
    public Color playerTrailEnd = new Color(0.92f, 0.95f, 1f, 0.85f);

    [Header("位置微调")]
    [Tooltip("加在命中点上。Y 往上抬可离开脚底/刀尖。")]
    public Vector3 offset = Vector3.zero;
    [Tooltip("沿镜头往外拉一点，避免埋进刀模。不要太大，否则会离开碰撞点。")]
    public float pullTowardCamera = 0.08f;

    // 订阅事件
    private void OnEnable()
    {
        CombatEventBus.OnWeaponDeflected += SpawnDeflectFX;
        CombatEventBus.OnAttackSwingStart += HandleSwingStart;
        CombatEventBus.OnAttackSwingEnd += HandleSwingEnd;
    }

    private void OnDisable()
    {
        CombatEventBus.OnWeaponDeflected -= SpawnDeflectFX;
        CombatEventBus.OnAttackSwingStart -= HandleSwingStart;
        CombatEventBus.OnAttackSwingEnd -= HandleSwingEnd;
    }

    // 处理事件
    private void SpawnDeflectFX(Vector3 hitPoint, DeflectType type)
    {
        GameObject prefabToSpawn = null;

        // 根据传入的枚举类型，决定使用哪个特效
        switch (type)
        {
            case DeflectType.Normal:
                prefabToSpawn = normalBlockSparks;
                break;
                
            case DeflectType.Perfect:
                prefabToSpawn = perfectParrySparks;
                break;
        }

        if (prefabToSpawn != null)
            Instantiate(prefabToSpawn, ResolveSpawnPos(hitPoint), Quaternion.identity);
    }

    Vector3 ResolveSpawnPos(Vector3 hitPoint)
    {
        Vector3 pos = hitPoint + offset;
        Camera cam = Camera.main;
        if (cam != null && pullTowardCamera != 0f)
            pos -= cam.transform.forward * pullTowardCamera;
        return pos;
    }

    static bool IsPlayer(CharacterBody body)
    {
        return body != null && body.GetComponent<PlayerBrain>() != null;
    }

    void HandleSwingStart(CharacterBody attacker)
    {
        if (!IsPlayer(attacker) || attacker.Weapon == null) return;

        WeaponSwingTrail trail = attacker.Weapon.GetComponent<WeaponSwingTrail>();
        if (trail == null)
            trail = attacker.Weapon.gameObject.AddComponent<WeaponSwingTrail>();
        trail.Setup(playerSwingTrailMat);
        trail.Begin(playerTrailStart, playerTrailEnd);
    }

    void HandleSwingEnd(CharacterBody attacker)
    {
        if (!IsPlayer(attacker) || attacker.Weapon == null) return;
        WeaponSwingTrail trail = attacker.Weapon.GetComponent<WeaponSwingTrail>();
        if (trail != null)
            trail.End();
    }
}