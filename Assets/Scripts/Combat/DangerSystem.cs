using UnityEngine;

/// <summary>
/// 危字判定系统，管理三种危字类型（突刺/扫击/投技）的检测与提示。
/// 检测成功后触发 OnDangerWarning 事件通知 UI。
/// </summary>
public class DangerSystem : MonoBehaviour
{
    #region Serialized Fields

    [Header("识破参数")]
    [SerializeField] private float _mikiriMaxDistance = 3f;
    [SerializeField] private float _mikiriMaxAngle = 60f;

    [Header("踩头参数")]
    [SerializeField] private float _stompHorizontalRange = 1.5f;
    [SerializeField] private float _stompVerticalOffset = 0.5f;

    #endregion

    #region Runtime State

    private AttackType _currentDangerType;
    private Transform _currentDangerSource;
    private bool _hasActiveDanger;
    private float _dangerStartTime;

    #endregion

    #region Properties

    /// <summary>是否有激活的危字</summary>
    public bool HasActiveDanger => _hasActiveDanger;

    /// <summary>当前危字类型</summary>
    public AttackType CurrentDangerType => _currentDangerType;

    /// <summary>当前危字来源（Boss Transform）</summary>
    public Transform CurrentDangerSource => _currentDangerSource;

    #endregion

    #region Public API

    /// <summary>
    /// 激活危字提示。由 Boss 攻击状态在进入 Startup 时调用。
    /// </summary>
    /// <param name="type">危字类型（突刺/扫击/投技）</param>
    /// <param name="source">攻击来源 Transform</param>
    public void ActivateDanger(AttackType type, Transform source)
    {
        _currentDangerType = type;
        _currentDangerSource = source;
        _hasActiveDanger = true;
        _dangerStartTime = Time.time;

        CombatEvents.RaiseDangerWarning(type);
    }

    /// <summary>
    /// 取消危字提示。Boss 攻击进入 Recovery 时调用。
    /// </summary>
    public void DeactivateDanger()
    {
        _hasActiveDanger = false;
        _currentDangerSource = null;
    }

    /// <summary>
    /// 检测识破条件：当前必须是突刺危，距离和角度在范围内。
    /// </summary>
    /// <param name="playerPos">玩家位置</param>
    /// <param name="playerForward">玩家朝向</param>
    /// <returns>是否可以触发识破</returns>
    public bool CanMikiri(Vector3 playerPos, Vector3 playerForward)
    {
        if (!_hasActiveDanger) return false;
        if (_currentDangerType != AttackType.Thrust) return false;
        if (_currentDangerSource == null) return false;

        float distance = Vector3.Distance(playerPos, _currentDangerSource.position);
        if (distance > _mikiriMaxDistance) return false;

        Vector3 toPlayer = (playerPos - _currentDangerSource.position).normalized;
        Vector3 bossForward = _currentDangerSource.forward;
        float angle = Vector3.Angle(toPlayer, bossForward);
        if (angle > _mikiriMaxAngle) return false;

        return true;
    }

    /// <summary>
    /// 检测踩头条件：玩家在 Boss 上方且水平距离在范围内。
    /// </summary>
    /// <param name="playerPos">玩家位置</param>
    /// <param name="playerVelY">玩家垂直速度（用于判定下落阶段）</param>
    /// <returns>是否可以触发踩头</returns>
    public bool CanStomp(Vector3 playerPos, float playerVelY)
    {
        if (!_hasActiveDanger) return false;
        if (_currentDangerType != AttackType.Sweep) return false;
        if (_currentDangerSource == null) return false;

        // 必须在下降阶段
        if (playerVelY >= 0f) return false;

        Vector3 bossPos = _currentDangerSource.position;
        float verticalDist = bossPos.y - playerPos.y;

        // 玩家在 Boss 头顶附近
        if (verticalDist < -_stompVerticalOffset || verticalDist > _stompVerticalOffset)
            return false;

        // 水平距离在范围内
        Vector3 playerXZ = new Vector3(playerPos.x, 0f, playerPos.z);
        Vector3 bossXZ = new Vector3(bossPos.x, 0f, bossPos.z);
        float horizontalDist = Vector3.Distance(playerXZ, bossXZ);

        return horizontalDist <= _stompHorizontalRange;
    }

    #endregion
}
