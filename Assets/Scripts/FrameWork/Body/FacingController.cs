using UnityEngine;

namespace ARPG.FrameWork.Body
{

    // 管理角色水平朝向、攻击转向和朝向冻结，通过角色标志判断能否转动。
    // OnAnimatorMove 按临时对齐、攻击转向、根旋转抑制、动画增量的优先级应用朝向。
    public class FacingController
    {
        private readonly CharacterBody body;

        public FacingController(CharacterBody body)
        {
            this.body = body;
        }

        // yaw 指绕竖直 Y 轴的水平朝向。下面的标志只控制旋转，不关闭根运动位移。
        // 攻击转向窗：丢掉 Clip yaw，否则挥砍根旋转会把刚对准的朝向拧走。
        private bool suppressRootYaw;
        private bool steerYawActive;
        private Vector3 steerYawDir;
        private float steerYawSpeed;
        // 识破打断后继续锁水平朝向，直到下一招；硬直一结束走位就会对准玩家猛转。
        public bool IsCombatYawFrozen { get; private set; }
        private int facingHoldStateHash;
        private Vector3 facingHoldDir;

        // 水平转向（度/秒）。刚体冻结旋转后只改 transform，避免和插值抢 yaw
        public void RotateYaw(Vector3 worldDir, float degreesPerSecond)
        {
            // 攻击/硬直/识破后冻结：走位节点的 RotateYaw 不走 Command，必须在这里拦住。
            if (suppressRootYaw || body.IsParried || body.IsFinisherLocked || IsCombatYawFrozen) return;
            if (worldDir.sqrMagnitude < 0.01f) return;
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.01f) return;
            Quaternion target = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
            // 度/秒乘帧时长得到本帧最多旋转的角度，使转向速度不随帧率改变。
            body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, target, degreesPerSecond * Time.deltaTime);
        }

        // 立即水平朝向（忍杀开演前对齐，不用每帧转）。
        // holdUntilState：Animator 还没切到该状态前，每帧 OnAnimatorMove 后再 Snap 一次。
        public void SnapYaw(Vector3 worldDir, string holdUntilState = null)
        {
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.0001f) return;
            Vector3 dir = worldDir.normalized;
            ApplyYaw(dir);
            if (!string.IsNullOrEmpty(holdUntilState))
            {
                facingHoldDir = dir;
                facingHoldStateHash = UnityEngine.Animator.StringToHash(holdUntilState);
            }
            else
            {
                facingHoldStateHash = 0;
            }
        }

        private void ApplyYaw(Vector3 worldDir)
        {
            body.transform.rotation = Quaternion.LookRotation(worldDir, Vector3.up);
            body.Rb.rotation = body.transform.rotation;
        }

        // 攻击转向：在 OnAnimatorMove 里转，才能盖过同一帧的 Clip 根旋转
        public void SetSteerYaw(Vector3 worldDir, float degreesPerSecond)
        {
            // 此处记录请求，实际转动留到 ApplyRootRotation，统一处理代码朝向与动画根旋转的先后。
            worldDir.y = 0f;
            if (worldDir.sqrMagnitude < 0.01f || degreesPerSecond <= 0f)
            {
                ClearSteerYaw();
                return;
            }

            steerYawDir = worldDir.normalized;
            steerYawSpeed = degreesPerSecond;
            steerYawActive = true;
        }

        public void ClearSteerYaw()
        {
            steerYawActive = false;
            steerYawDir = Vector3.zero;
        }

        public void SetSuppressRootYaw(bool suppress)
        {
            suppressRootYaw = suppress;
            if (!suppress)
            {
                ClearSteerYaw();
            }
        }

        // 钉住当前水平朝向：清掉 Snap 残留 hold，丢掉之后的 Root yaw / 走位转向。
        public void FreezeCombatYaw()
        {
            facingHoldStateHash = 0;
            facingHoldDir = Vector3.zero;
            ClearSteerYaw();
            Vector3 fwd = body.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                ApplyYaw(fwd.normalized);
            }
            suppressRootYaw = true;
            IsCombatYawFrozen = true;
        }

        public void ClearCombatYawFrozen()
        {
            IsCombatYawFrozen = false;
        }

        private void ApplySteerYaw()
        {
            float dt = Time.deltaTime;
            Vector3 currentFwd = body.transform.forward;
            currentFwd.y = 0f;
            if (currentFwd.sqrMagnitude < 0.0001f)
            {
                ApplyYaw(steerYawDir);
                return;
            }

            Quaternion current = Quaternion.LookRotation(currentFwd.normalized, Vector3.up);
            Quaternion target = Quaternion.LookRotation(steerYawDir, Vector3.up);
            Quaternion next = Quaternion.RotateTowards(current, target, steerYawSpeed * dt);
            Vector3 nextFwd = next * Vector3.forward;
            nextFwd.y = 0f;
            if (nextFwd.sqrMagnitude > 0.0001f)
            {
                ApplyYaw(nextFwd.normalized);
            }
        }

        private void FlattenYaw()
        {
            Vector3 fwd = body.transform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
            {
                ApplyYaw(fwd.normalized);
            }
        }

        // CharacterBody.OnAnimatorMove 在根位移写入后调用。
        // 优先级：成对动作临时对齐 > 攻击追踪转向 > 抑制根旋转 > 动画自己的根旋转。
        public void ApplyRootRotation(Animator animator)
        {
            bool holdFacing = false;
            if (facingHoldStateHash != 0)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                if (info.shortNameHash == facingHoldStateHash)
                {
                    facingHoldStateHash = 0;
                }
                else
                {
                    holdFacing = true;
                }
            }

            if (holdFacing)
            {
                ApplyYaw(facingHoldDir);
            }
            else if (steerYawActive)
            {
                ApplySteerYaw();
            }
            else if (suppressRootYaw)
            {
                // 转向窗外仍锁水平朝向：挥砍后半段的 Root yaw 不会把起手对准拧偏
                FlattenYaw();
            }
            else
            {
                body.transform.rotation *= animator.deltaRotation;
            }
        }
    }

}
