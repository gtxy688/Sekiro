using UnityEngine;

// 角色战斗属性配置（SO）：玩家和 Boss 共用 CharacterBody，数值各自配置一份
// 战斗数值全部走 SO，禁止在 .cs 里硬编码
[CreateAssetMenu(fileName = "NewCharacterConfig", menuName = "Combat/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("血量")]
    public int MaxHP = 1000;          // 最大血量
    public int HealAmount = 300;      // 葫芦每次回复量

    [Header("架势")]
    public float MaxPosture = 100f;   // 最大架势
    public float PostureDecayRate = 20f;  // 架势自然回复速度（每秒）
    public float PostureDecayDelay = 2f;  // 停止受击多少秒后开始回复

    [Header("受击")]
    public float StunDuration = 0.5f; // 硬直时长

    [Header("受击动画（接口预留，留空 = 回退到普通受击动画）")]
    public string HurtAnim_Normal = "Hurt_Ground";     // 裸吃普通攻击
    public string HurtAnim_Heavy = "Hurt_Heavy";       // 强力招式（击飞/倒地），空则用 HurtAnim_Normal
    public string HurtAnim_Guard = "Hurt_Guard";       // 格挡轻攻击，空则用 HurtAnim_Normal
    public string HurtAnim_GuardHeavy = "Hurt_GuardHeavy"; // 格挡重攻击（Knockback>0），空则用 HurtAnim_Guard
    public string HurtAnim_Deflected = "Deflected";    // 被完美弹反后的硬直，空则用 HurtAnim_Normal
    public string HurtAnim_Broken = "Stagger_Broken";  // 架势崩解倒地（占位名）

    [Header("防御/弹反（M4）")]
    public float DeflectWindow = 0.3f;             // 完美弹反窗口（秒）
    public float DeflectPostureGain = 30f;         // 完美弹反成功：攻击者涨的架势
    public float DeflectSelfPostureFactor = 0f;    // 已废弃：完美弹反不再涨自己架势，保留字段以免序列化丢失
    public float GuardPostureFactor = 0.5f;        // 格挡时自己涨架势的比例（×对方架势伤害）
    public float ParriedDuration = 0.35f;          // 被完美弹反后的硬直时长
    public float DeflectMashLimit = 3;             // 抖刀惩罚：0.5s 内连点次数阈值
    public float DeflectMashWindow = 0.5f;         // 抖刀判定窗口
    public float DeflectMashPenalty = 0.75f;       // 每次超限惩罚系数
    public float DeflectWindowMin = 0.1f;          // 弹反窗口下限
    public float GuardPostureRecoveryMultiplier = 5f; // 按住格挡 2s 后架势回复倍率

    [Header("架势（M9）")]
    public bool PostureDecayInverse = false;   // true=非线性（架势越高回越慢，Boss 用）；false=线性
    public float PostureBrokenDuration = 5f;   // 仅文档/旧数据保留；攻击崩解窗口已改为跟倒地动画走

    [Header("命数（Boss 用，一阶段 2 条命）")]
    public int LifeCount = 1;      // 总命数（玩家填 1，Boss 填 2）

    [Header("闪避")]
    public float DodgeDuration = 0.5f; // 垫步持续时长（位移由动画根运动驱动，这里只控制总时长）
    public float DodgeIFrame = 0.3f;  // 垫步无敌帧时长（M4）

    [Header("识破（M17）")]
    public float MikiriDuration = 0.8f;      // 踩刀动画时长
    public float MikiriPostureGain = 30f;    // 识破成功涨攻击者架势

    [Header("移动")]
    // 移动速度由动画 Root 曲线决定（全权根运动），这里只留转身速度
    public float RotationSpeed = 720f;

    [Header("攻击输入")]
    public float AttackHoldDuration = 0.3f; // 按住攻击达到该时长后自动触发突刺

    [Header("葫芦/复活（玩家专属，Boss 用不到）")]
    public int GourdCount = 10;       // 初始葫芦次数
    public int ReviveCount = 1;       // 复活次数
}
