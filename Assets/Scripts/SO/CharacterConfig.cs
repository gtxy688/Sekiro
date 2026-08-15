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

    [Header("闪避")]
    public float DodgeDuration = 0.5f; // 垫步持续时长（位移由动画根运动驱动，这里只控制总时长）

    [Header("识破（M17）")]
    public float MikiriDuration = 0.8f;      // 踩刀动画时长
    public float MikiriPostureGain = 30f;    // 识破成功涨攻击者架势

    [Header("移动")]
    // 移动速度由动画 Root 曲线决定（全权根运动），这里只留转身速度
    public float RotationSpeed = 720f;

    [Header("葫芦/复活（玩家专属，Boss 用不到）")]
    public int GourdCount = 10;       // 初始葫芦次数
    public int ReviveCount = 1;       // 复活次数
}
