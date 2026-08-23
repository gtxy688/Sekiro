using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

// FreeLook 的鼠标环绕输入（绕过 CinemachineInputProvider，无需配置 Input Action）。
// 右键是防御，不能拿来转镜头；鼠标移动即转。垂直轴松手回中，避免锁定后视角卡在天上/地上。
// 挂在 FreeLook 上后，关掉同物体上的 CinemachineInputProvider；FreeLook 的 Axis Max Speed 保持 0。
// 锁定时由 CameraController 禁用本组件，避免和锁定相抢轴。
public class CinemachineOrbitInput : MonoBehaviour
{
    [Tooltip("水平旋转速度（度/像素）")]
    public float XSpeed = 0.12f;

    [Tooltip("垂直旋转速度（0~1 满轨/像素）")]
    public float YSpeed = 0.0015f;

    [Tooltip("松手后垂直轴回到这个高度（0~1，胸口略偏上）")]
    public float YCenter = 0.45f;

    [Tooltip("松手后回中速度，0 表示不回中")]
    public float YRecenterSpeed = 2f;

    private CinemachineFreeLook freeLook;

    private void Awake()
    {
        freeLook = GetComponent<CinemachineFreeLook>();
    }

    private void Update()
    {
        if (freeLook == null || GamePause.IsPaused) return;
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 delta = mouse.delta.ReadValue();
        if (delta.sqrMagnitude > 0.0001f)
        {
            float x = freeLook.m_XAxis.Value + delta.x * XSpeed;
            if (x > 180f) x -= 360f;
            else if (x < -180f) x += 360f;
            freeLook.m_XAxis.Value = x;

            freeLook.m_YAxis.Value += delta.y * YSpeed;
            freeLook.m_YAxis.Value = Mathf.Clamp(freeLook.m_YAxis.Value, 0.02f, 0.98f);
        }
        else if (YRecenterSpeed > 0f)
        {
            freeLook.m_YAxis.Value = Mathf.MoveTowards(
                freeLook.m_YAxis.Value, YCenter, YRecenterSpeed * Time.deltaTime);
        }
    }
}
