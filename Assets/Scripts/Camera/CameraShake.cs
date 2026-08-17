using DG.Tweening;
using UnityEngine;

// 相机震动（M12 表现）：订阅 CombatEventBus.OnCameraShake，
// 对主相机做随机偏移抖动（不依赖 Cinemachine Impulse，零配置）
// 挂 Main Camera 上。强度来自触发方（弹反小抖/崩解大抖/处决强抖）
public class CameraShake : MonoBehaviour
{
    [Header("基础参数")]
    public float baseDuration = 0.2f;   // 抖动时长（最终 = 基础 × 强度）
    public float baseMagnitude = 0.12f; // 抖动幅度（最终 = 基础 × 强度）

    private Vector3 originalPos;
    private Tween shakeTween;

    private void Awake()
    {
        originalPos = transform.localPosition;
    }

    private void OnEnable()
    {
        CombatEventBus.OnCameraShake += HandleShake;
    }

    private void OnDisable()
    {
        CombatEventBus.OnCameraShake -= HandleShake;
    }

    private void HandleShake(float intensity)
    {
        if (intensity <= 0f) return;

        shakeTween?.Kill();
        transform.localPosition = originalPos;

        shakeTween = transform.DOShakePosition(
                baseDuration * intensity,
                baseMagnitude * intensity,
                vibrato: 15,
                randomness: 90f,
                fadeOut: true)
            .OnComplete(() => transform.localPosition = originalPos);
    }
}
