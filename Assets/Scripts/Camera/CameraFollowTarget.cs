using UnityEngine;

namespace ARPG.Camera
{

    // 相机跟随点：位置硬跟上玩家，不平滑。
    // 平滑会让镜头慢半拍，人和镜头错开，画面发糊（老花眼）。
    // 存在的意义只有一个：不把玩家动画 yaw 传给相机（旋转保持 identity，锁定时才朝 Boss）。
    // 不要挂成玩家子物体。
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
            if (source == null) return;

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
