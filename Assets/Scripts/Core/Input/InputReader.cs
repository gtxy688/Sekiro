using UnityEngine;

/// <summary>
/// 输入读取器，读取玩家输入并暴露查询方法供 PlayerController 调用。
/// 通过 <see cref="IInputProvider"/> 接口抽象化输入来源，支持新 Input System 和旧 Input Manager。
/// 按键映射由 IInputProvider 实现类负责，此类不硬编码任何按键名。
/// </summary>
/// <remarks>
/// <para>架构设计：</para>
/// <list type="bullet">
///   <item>纯 C# 类，无 MonoBehaviour 依赖，便于 EditMode 测试</item>
///   <item>通过 IInputProvider 接口解耦输入来源（新 Input System / 旧 Input Manager / 测试模拟）</item>
///   <item>MonoBehaviour 包装器（InputReaderComponent）在独立程序集中管理生命周期</item>
/// </list>
/// <para>使用方式：</para>
/// <code>
/// // 测试环境
/// var reader = new InputReader(testProvider);
/// reader.Update();
/// if (reader.IsAttackPressed()) { ... }
///
/// // 生产环境（由 InputReaderComponent 自动管理）
/// _inputReader.Update();
/// Vector2 move = _inputReader.GetMoveInput();
/// </code>
/// </remarks>
public class InputReader
{
    private IInputProvider _provider;

    /// <summary>
    /// 创建 InputReader 实例，使用指定的输入提供者
    /// </summary>
    /// <param name="provider">输入提供者实现，为 null 时自动创建旧 Input Manager 提供者</param>
    public InputReader(IInputProvider provider = null)
    {
        _provider = provider ?? CreateDefaultProvider();
    }

    /// <summary>
    /// 获取或设置当前的输入提供者
    /// </summary>
    public IInputProvider Provider
    {
        get => _provider;
        set => _provider = value;
    }

    /// <summary>
    /// 每帧调用以更新输入状态。由 InputReaderComponent 或测试代码调用。
    /// </summary>
    public void Update()
    {
        _provider?.Update();
    }

    #region 查询方法

    /// <summary>
    /// 攻击键是否在本帧被按下（鼠标左键）。
    /// 用于触发 3 段连斩或忍杀（架势归零时自动切换）。
    /// </summary>
    /// <returns>本帧按下返回 true</returns>
    public bool IsAttackPressed() =>
        _provider != null && _provider.IsPressed(CombatInput.Attack);

    /// <summary>
    /// 弹刀键是否在本帧被按下（鼠标右键轻点）。
    /// 完美弹刀需要在攻击命中的精确时机按下。
    /// </summary>
    /// <returns>本帧按下返回 true</returns>
    public bool IsDeflectPressed() =>
        _provider != null && _provider.IsPressed(CombatInput.Deflect);

    /// <summary>
    /// 弹刀键是否被按住（鼠标右键按住）。
    /// 按住时进入持续格挡状态，松开后恢复。
    /// </summary>
    /// <returns>按住时返回 true</returns>
    public bool IsDeflectHeld() =>
        _provider != null && _provider.IsHeld(CombatInput.Deflect);

    /// <summary>
    /// 获取 WASD 移动输入向量
    /// </summary>
    /// <returns>归一化的 2D 移动向量，X 为水平方向，Y 为垂直方向</returns>
    public Vector2 GetMoveInput() =>
        _provider != null ? _provider.GetMoveInput() : Vector2.zero;

    /// <summary>
    /// 闪避键是否在本帧被按下（Shift）。
    /// 普通状态下为闪避，突刺危字时自动切换为识破。
    /// </summary>
    /// <returns>本帧按下返回 true</returns>
    public bool IsDodgePressed() =>
        _provider != null && _provider.IsPressed(CombatInput.Dodge);

    /// <summary>
    /// 跳跃键是否在本帧被按下（空格）
    /// </summary>
    /// <returns>本帧按下返回 true</returns>
    public bool IsJumpPressed() =>
        _provider != null && _provider.IsPressed(CombatInput.Jump);

    /// <summary>
    /// 回血键是否在本帧被按下（E 键，药葫芦）
    /// </summary>
    /// <returns>本帧按下返回 true</returns>
    public bool IsHealPressed() =>
        _provider != null && _provider.IsPressed(CombatInput.Heal);

    /// <summary>
    /// 锁定键是否在本帧被按下（鼠标中键，锁定 Boss）
    /// </summary>
    /// <returns>本帧按下返回 true</returns>
    public bool IsLockOnPressed() =>
        _provider != null && _provider.IsPressed(CombatInput.LockOn);

    /// <summary>
    /// 获取指定战斗输入的按下状态（通用查询）
    /// </summary>
    /// <param name="input">战斗输入类型</param>
    /// <returns>本帧按下返回 true</returns>
    public bool IsPressed(CombatInput input) =>
        _provider != null && _provider.IsPressed(input);

    /// <summary>
    /// 获取指定战斗输入的按住状态（通用查询）
    /// </summary>
    /// <param name="input">战斗输入类型</param>
    /// <returns>按住时返回 true</returns>
    public bool IsHeld(CombatInput input) =>
        _provider != null && _provider.IsHeld(input);

    #endregion

    /// <summary>
    /// 创建默认的旧 Input Manager 提供者作为备选方案。
    /// 当未注入自定义提供者时自动调用。
    /// </summary>
    /// <returns>旧 Input Manager 提供者实例</returns>
    private static IInputProvider CreateDefaultProvider()
    {
        return new LegacyInputProvider();
    }
}
