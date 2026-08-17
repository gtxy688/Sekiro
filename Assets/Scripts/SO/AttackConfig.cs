using UnityEngine;

// 这个标签让你可以在 Unity 项目的右键菜单里直接创建这个配置文件
[CreateAssetMenu(fileName = "NewAttackConfig", menuName = "Combat/Attack Configuration")]
public class AttackConfig : ScriptableObject
{
    [Header("表现配置")]
    public string AnimName;             // 动画状态机里的名字
    public float TransitionDuration = 0.1f; // 动画过渡时间

    [Header("战斗数值")]
    public int BaseDamage = 10;         // 基础伤害
    public float PostureDamage = 15f;   // 躯干伤害
    public float Knockback = 0f;        // 击退强度（0=普通受击；>0 对方播 Heavy 受击/击飞动画，接口预留）

    [Header("危字攻击（M17）")]
    // 危字攻击不可被普通防御/弹反抵挡，玩家必须用对应方式应对
    public PerilousType Perilous = PerilousType.None;

    [Header("连招窗口期")]
    public float StateDuration = 1.633f;  // 这个动作总共持续多久
    public float ComboWindowStart = 0f; // 挥刀多久后允许按键
    public float ComboWindowEnd = 0.33f;   // 多久之后按键无效（错过连招）

    [Header("连招派生")]
    // 极其关键：指向下一段攻击的配置！如果没有下一段，留空即可
    public AttackConfig NextCombo;      
}