using UnityEngine;

/// <summary>
/// 战斗输入枚举，按优先级从高到低排列。
/// 数值越小优先级越高，用于输入优先级排序。
/// </summary>
public enum CombatInput
{
    /// <summary>忍杀（最高优先级）</summary>
    Deathblow = 0,

    /// <summary>弹刀/格挡</summary>
    Deflect = 1,

    /// <summary>松开格挡</summary>
    DeflectRelease = 2,

    /// <summary>攻击</summary>
    Attack = 3,

    /// <summary>闪避</summary>
    Dodge = 4,

    /// <summary>识破</summary>
    Mikiri = 5,

    /// <summary>跳跃</summary>
    Jump = 6,

    /// <summary>回血</summary>
    Heal = 7,

    /// <summary>锁定切换</summary>
    LockOn = 8,

    /// <summary>移动（最低优先级）</summary>
    Move = 9
}

/// <summary>
/// 输入提供者接口，抽象化输入读取方式。
/// 实现此接口以对接不同的输入系统（新 Input System 或旧 Input Manager）。
/// 所有按键映射在实现类中定义，InputReader 不关心具体按键。
/// </summary>
public interface IInputProvider
{
    /// <summary>
    /// 检查指定战斗输入是否在本帧被按下（仅触发帧为 true）
    /// </summary>
    /// <param name="input">战斗输入类型</param>
    /// <returns>本帧是否被按下</returns>
    bool IsPressed(CombatInput input);

    /// <summary>
    /// 检查指定战斗输入是否被按住（持续为 true）
    /// </summary>
    /// <param name="input">战斗输入类型</param>
    /// <returns>是否被按住</returns>
    bool IsHeld(CombatInput input);

    /// <summary>
    /// 获取移动输入向量（WASD），X 为水平方向，Y 为垂直方向
    /// </summary>
    /// <returns>归一化的 2D 移动向量</returns>
    Vector2 GetMoveInput();

    /// <summary>
    /// 每帧更新输入状态，由 InputReader 调用
    /// </summary>
    void Update();
}
