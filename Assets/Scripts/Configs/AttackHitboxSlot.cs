// 本招/本段用哪把 Hitbox。配置只写枚举，场景引用在 CharacterBody 上。
public enum AttackHitboxSlot
{
    Weapon = 0, // 刀（默认）
    Elbow = 1,
    Kick = 2    // 预留，本需求不接线
}
