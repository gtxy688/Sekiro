using UnityEngine;
using UnityEngine.InputSystem;
using Cinemachine;

// FreeLook 的鼠标拖拽环绕输入（绕过 CinemachineInputProvider，无需配置 Input Action）
// 操作：按住鼠标右键拖拽 = 旋转视角（与策划案一致：右键短按弹反 / 长按格挡+拖拽转视角）
public class CinemachineOrbitInput : MonoBehaviour
{
    [Tooltip("水平旋转速度（度/像素）")]
    public float XSpeed = 0.12f;

    [Tooltip("垂直旋转速度（0~1 满轨/像素）")]
    public float YSpeed = 0.0015f;

    private CinemachineFreeLook freeLook;

    private void Awake()
    {
        freeLook = GetComponent<CinemachineFreeLook>();
    }

    private void Update()
    {
        if (freeLook == null) return;
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // 只在按住右键拖拽时转视角（松开或点击不转，避免误触）
        if (!mouse.rightButton.isPressed) return;

        Vector2 delta = mouse.delta.ReadValue();

        // X 轴：水平环绕（0~360°），手动回绕防止撞到轴范围边界卡住
        float x = freeLook.m_XAxis.Value + delta.x * XSpeed;
        if (x > 180f) x -= 360f;
        else if (x < -180f) x += 360f;
        freeLook.m_XAxis.Value = x;

        // Y 轴：0=最底轨道（贴地） 1=最高轨道（俯视），限位
        freeLook.m_YAxis.Value += delta.y * YSpeed;
        freeLook.m_YAxis.Value = Mathf.Clamp(freeLook.m_YAxis.Value, 0.02f, 0.98f);
    }
}