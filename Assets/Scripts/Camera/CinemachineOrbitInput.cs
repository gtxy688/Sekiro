using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

// FreeLook 环绕输入（关掉 CinemachineInputProvider；FreeLook Axis Max Speed = 0）。
// 键鼠：鼠标移动即转（右键是防御，不能拿来转镜头）。
// 手柄：右摇杆转镜头。垂直轴松手回中，避免锁定后视角卡在天上/地上。
// 锁定时由 CameraController 禁用本组件，避免和锁定相抢轴。
public class CinemachineOrbitInput : MonoBehaviour
{
    [Tooltip("水平旋转速度（度/像素）")]
    public float XSpeed = 0.12f;

    [Tooltip("垂直旋转速度（0~1 满轨/像素）")]
    public float YSpeed = 0.0015f;

    [Tooltip("手柄右摇杆水平速度（度/秒，满偏）")]
    public float StickXSpeed = 140f;

    [Tooltip("手柄右摇杆垂直速度（满轴/秒，满偏）")]
    public float StickYSpeed = 0.9f;

    [Tooltip("右摇杆死区")]
    public float StickDeadzone = 0.2f;

    [Tooltip("松手后垂直轴回到这个高度（0~1，胸口略偏上）")]
    public float YCenter = 0.45f;

    [Tooltip("松手后回中速度，0 表示不回中")]
    public float YRecenterSpeed = 2f;

    private CinemachineFreeLook freeLook;
    private float ignoreLookUntil;

    public void IgnoreLookUntil(float unscaledTime)
    {
        ignoreLookUntil = unscaledTime;
    }

    private void Awake()
    {
        freeLook = GetComponent<CinemachineFreeLook>();
        ignoreLookUntil = Time.unscaledTime + 0.45f;
    }

    private void Update()
    {
        if (freeLook == null || GamePause.IsPaused) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;
        if (Time.unscaledTime < ignoreLookUntil) return;

        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
        Vector2 stick = ReadRightStick();
        bool hasLook = mouseDelta.sqrMagnitude > 0.0001f || stick.sqrMagnitude > 0.0001f;

        if (hasLook)
        {
            float xDelta = mouseDelta.x * XSpeed + stick.x * StickXSpeed * Time.deltaTime;
            float yDelta = mouseDelta.y * YSpeed + stick.y * StickYSpeed * Time.deltaTime;

            float x = freeLook.m_XAxis.Value + xDelta;
            if (x > 180f) x -= 360f;
            else if (x < -180f) x += 360f;
            freeLook.m_XAxis.Value = x;

            freeLook.m_YAxis.Value += yDelta;
            freeLook.m_YAxis.Value = Mathf.Clamp(freeLook.m_YAxis.Value, 0.02f, 0.98f);
        }
        else if (YRecenterSpeed > 0f)
        {
            freeLook.m_YAxis.Value = Mathf.MoveTowards(
                freeLook.m_YAxis.Value, YCenter, YRecenterSpeed * Time.deltaTime);
        }
    }

    private Vector2 ReadRightStick()
    {
        Gamepad gamepad = Gamepad.current;
        if (gamepad == null) return Vector2.zero;

        Vector2 raw = gamepad.rightStick.ReadValue();
        float dead = StickDeadzone;
        if (raw.sqrMagnitude < dead * dead) return Vector2.zero;
        return raw;
    }
}
