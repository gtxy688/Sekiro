using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 输入读取组件，挂载在玩家 GameObject 上。
/// 使用 Unity 新 Input System（.inputactions 资产文件）读取输入。
/// 资产文件在 Resources/Input/PlayerInputActions.inputactions，可在 Editor 中双击编辑按键绑定。
/// </summary>
public class InputReaderComponent : MonoBehaviour
{
    [Header("输入资产")]
    [Tooltip("双击可编辑按键绑定")]
    [SerializeField] private InputActionAsset _inputActions;

    private InputActionMap _gameplayMap;

    #region Actions

    private InputAction _moveAction;
    private InputAction _attackAction;
    private InputAction _deflectAction;
    private InputAction _dodgeAction;
    private InputAction _jumpAction;
    private InputAction _healAction;
    private InputAction _lockOnAction;

    #endregion

    private void Awake()
    {
        if (_inputActions == null)
            _inputActions = Resources.Load<InputActionAsset>("Input/PlayerInputActions");

        if (_inputActions == null)
        {
            Debug.LogError("未找到 PlayerInputActions.inputactions，请确保文件在 Resources/Input/ 目录下");
            return;
        }

        _gameplayMap = _inputActions.FindActionMap("Gameplay");
        if (_gameplayMap == null)
        {
            Debug.LogError("InputActionAsset 中缺少 Gameplay ActionMap");
            return;
        }

        _moveAction = _gameplayMap.FindAction("Move");
        _attackAction = _gameplayMap.FindAction("Attack");
        _deflectAction = _gameplayMap.FindAction("Deflect");
        _dodgeAction = _gameplayMap.FindAction("Dodge");
        _jumpAction = _gameplayMap.FindAction("Jump");
        _healAction = _gameplayMap.FindAction("Heal");
        _lockOnAction = _gameplayMap.FindAction("LockOn");
    }

    private void OnEnable()
    {
        _gameplayMap?.Enable();
    }

    private void OnDisable()
    {
        _gameplayMap?.Disable();
    }

    #region 查询方法代理

    /// <summary>攻击键是否在本帧被按下（鼠标左键）</summary>
    public bool IsAttackPressed() =>
        _attackAction != null && _attackAction.WasPressedThisFrame();

    /// <summary>弹刀键是否在本帧被按下（鼠标右键轻点）</summary>
    public bool IsDeflectPressed() =>
        _deflectAction != null && _deflectAction.WasPressedThisFrame();

    /// <summary>弹刀键是否被按住（鼠标右键按住）</summary>
    public bool IsDeflectHeld() =>
        _deflectAction != null && _deflectAction.IsPressed();

    /// <summary>获取 WASD 移动输入向量</summary>
    public Vector2 GetMoveInput() =>
        _moveAction != null ? _moveAction.ReadValue<Vector2>() : Vector2.zero;

    /// <summary>闪避键是否在本帧被按下（左 Shift）</summary>
    public bool IsDodgePressed() =>
        _dodgeAction != null && _dodgeAction.WasPressedThisFrame();

    /// <summary>跳跃键是否在本帧被按下（空格）</summary>
    public bool IsJumpPressed() =>
        _jumpAction != null && _jumpAction.WasPressedThisFrame();

    /// <summary>回血键是否在本帧被按下（E 键）</summary>
    public bool IsHealPressed() =>
        _healAction != null && _healAction.WasPressedThisFrame();

    /// <summary>锁定键是否在本帧被按下（鼠标中键）</summary>
    public bool IsLockOnPressed() =>
        _lockOnAction != null && _lockOnAction.WasPressedThisFrame();

    #endregion
}
