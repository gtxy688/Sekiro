using UnityEngine;

namespace ARPG.Camera
{

    // 同步玩家位置并隔离动画旋转，锁定时才朝向目标；保持独立于玩家层级。
    // 位置直接跟随，避免角色与相机之间出现额外延迟。
    [DefaultExecutionOrder(-100)]
    public class CameraFollowTarget : MonoBehaviour
    {
        public Transform source;
        [Tooltip("相对角色脚底的高度（胸口）")]
        public float height = 1.4f;

        private Transform yawTarget;

        public void SetYawTarget(Transform target)
        {
            yawTarget = target;
            if (yawTarget == null)
                transform.rotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            Vector3 p = source.position;
            p.y += height;
            transform.position = p;

            if (yawTarget != null)
            {
                Vector3 dir = yawTarget.position - source.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
            else
            {
                transform.rotation = Quaternion.identity;
            }
        }
    }

}
