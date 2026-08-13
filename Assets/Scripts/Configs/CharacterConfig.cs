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

    [Header("移动")]
    public float MoveSpeed = 4f;
    public float RotationSpeed = 720f;

    [Header("葫芦/复活（玩家专属，Boss 用不到）")]
    public int GourdCount = 10;       // 初始葫芦次数
    public int ReviveCount = 1;       // 复活次数
}
