using UnityEngine;

// 挂载在武器模型（如剑刃）上的碰撞检测脚本
[RequireComponent(typeof(Collider))]
public class WeaponHitbox : MonoBehaviour
{
    [Header("武器属性")]
    public int baseDamage = 10;
    public float postureDamage = 15f; // 躯干值/架势槽伤害

    private Collider hitboxCollider;
    private CharacterBody ownerBody; // 武器的主人

    private void Awake()
    {
        hitboxCollider = GetComponent<Collider>();
        hitboxCollider.enabled = false; // 默认关闭，绝对不能一直开着！
    }

    public void Initialize(CharacterBody owner)
    {
        this.ownerBody = owner;
    }

    // 由状态机或动画事件来调用：开启伤害判定
    public void EnableHitbox() => hitboxCollider.enabled = true;

    // 由状态机或动画事件来调用：关闭伤害判定
    public void DisableHitbox() => hitboxCollider.enabled = false;

    // 核心的碰撞检测逻辑
    private void OnTriggerEnter(Collider other)
    {
        // 1. 排除打到自己
        if (other.gameObject == ownerBody.gameObject) return;

        // 2. 尝试获取受击方身上的身体组件
        // 实战中通常会给受击盒(Hurtbox)专门写个接口，比如 IDamageable
        CharacterBody targetBody = other.GetComponentInParent<CharacterBody>();
        
        if (targetBody != null)
        {
            // 3. 把伤害数据打包，丢给受击方处理
            // 这里我们传递了武器的主人，方便做“弹反时的受击方反噬”
            targetBody.ReceiveHit(ownerBody, baseDamage, postureDamage, transform.position);
            
            // 可选：如果是不穿透的武器，打中一次后立刻关闭碰撞盒
            // DisableHitbox(); 
        }
    }
}