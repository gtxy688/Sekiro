using UnityEngine;

/// <summary>
/// 帧冻结管理器，受击时暂停所有逻辑（物理/FSM/动画），仅渲染继续。
/// 通过设置 Time.timeScale = 0 并定时恢复实现。
/// </summary>
public class HitStopManager : MonoBehaviour
{
    #region Runtime State

    private float _remainingTime;
    private bool _isActive;

    #endregion

    #region Properties

    /// <summary>是否处于帧冻结状态</summary>
    public bool IsActive => _isActive;

    /// <summary>剩余冻结时间（秒）</summary>
    public float RemainingTime => _remainingTime;

    #endregion

    #region Unity Lifecycle

    /// <summary>
    /// 每帧最早执行：推进帧冻结计时器。
    /// 冻结期间所有逻辑更新由 Time.timeScale=0 暂停。
    /// </summary>
    private void Update()
    {
        if (!_isActive) return;

        _remainingTime -= Time.unscaledDeltaTime;

        if (_remainingTime <= 0f)
        {
            _isActive = false;
            Time.timeScale = 1f;
        }
    }

    #endregion

    #region Public API

    /// <summary>
    /// 触发帧冻结，持续指定时长。
    /// 冻结期间 Time.timeScale = 0，视觉渲染不受影响。
    /// </summary>
    /// <param name="duration">冻结时长（秒），如 0.033f=2帧@60fps</param>
    public void Trigger(float duration)
    {
        if (duration <= 0f) return;

        _remainingTime = duration;
        _isActive = true;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// 立即中止帧冻结，恢复时间流速。
    /// </summary>
    public void Stop()
    {
        _remainingTime = 0f;
        _isActive = false;
        Time.timeScale = 1f;
    }

    #endregion
}
