using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.Combat
{

    // 标记身体碰撞体所属的角色，供近战和箭矢查询后交给 CombatManager 结算。
    public class Hurtbox : MonoBehaviour
    {
        public CharacterBody Owner { get; private set; }

        private void Awake()
        {
            // 容错：没手动 Initialize 也能自动找到主人
            if (Owner == null)
                Owner = GetComponentInParent<CharacterBody>();
        }

        public void Initialize(CharacterBody owner)
        {
            Owner = owner;
        }
    }

}
