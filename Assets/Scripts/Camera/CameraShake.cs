using DG.Tweening;
using UnityEngine;

using ARPG.Mgr;
namespace ARPG.Camera
{

    // 挂在主相机上，订阅战斗震动事件，并按事件强度缩放局部位置抖动。
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

}
