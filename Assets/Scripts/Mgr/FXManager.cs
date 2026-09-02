using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork.Body;
using ARPG.Player;
namespace ARPG.Mgr
{

    public class FXManager : MonoBehaviour
    {
        [Header("特效预制体")]
        public GameObject normalBlockSparks;
        public GameObject perfectParrySparks;
        public Material playerSwingTrailMat;

        [Header("玩家刀光颜色")]
        [Tooltip("刀光线靠近刀尖")]
        [ColorUsage(true, true)]
        public Color playerTrailStart = Color.white;
        [Tooltip("刀光线尾端")]
        [ColorUsage(true, true)]
        public Color playerTrailEnd = new Color(0.75f, 0.88f, 1f, 1f);

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
            UnityEngine.Camera cam = UnityEngine.Camera.main;
            if (cam != null && pullTowardCamera != 0f)
                pos -= cam.transform.forward * pullTowardCamera;
            return pos;
        }

        // 原本这里有一份自己的实现（查 PlayerBrain 组件），与 HitReactionUtil 那套
        // （比 PlayerRef 引用）标准不同，两者可能给出矛盾答案。已统一到阵营字段。
        static bool IsPlayer(CharacterBody body) => HitReactionUtil.IsPlayer(body);

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
}
