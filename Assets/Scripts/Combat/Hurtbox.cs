using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.Combat
{

    // 受击盒：挂在角色身体上（单胶囊体即可，无部位区分需求）
    // 只标记"我是谁的主人"，具体判定/结算都交给 Hitbox → CombatManager
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
