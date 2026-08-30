using UnityEngine;

namespace ARPG.FrameWork.Body
{

    // 转向模块（CharacterBody 重构试点一）。
    // 职责：水平朝向的全部驱动与决策——
    //   1. 走位转向 RotateYaw（度/秒，带冻结/硬直拦截）
    //   2. 瞬间对齐 SnapYaw + Hold（Animator 未切到目标状态前每帧补一次）
    //   3. 攻击转向 SetSteerYaw（在 OnAnimatorMove 里转，盖过同帧 Clip 根旋转）
    //   4. Root yaw 抑制 SetSuppressRootYaw / FreezeCombatYaw（丢掉 Clip yaw，锁死朝向）
    //   5. OnAnimatorMove 的旋转优先级决策 ApplyRootRotation：Hold > 攻击转向 > Root 抑制 > 动画增量
    // 约束：
    // - 运行时状态（hold/steer/suppress/frozen）全部收在本类内部，CharacterBody 不再持有；
    // - CharacterBody 保留同名转发接口，全项目调用方零改动；
    // - 本模块没有任何序列化字段，CharacterBody 上的 Inspector 配置与 prefab 数据不受影响；
    // - 只读依赖 body 的 transform/Rb 与 IsParried/IsFinisherLocked 两个语义化标志
    //   （架构红线：不判状态类型，所以不碰状态机）。
    public class FacingController
    {
        private readonly CharacterBody body;

        public FacingController(CharacterBody body)
        {
            this.body = body;
        }

        // 攻击转向窗：吃 Root 位移，丢掉 Clip yaw，否则挥砍根旋转会把刚对准的朝向拧走
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
            if (body.Rb != null)
            {
                body.Rb.rotation = body.transform.rotation;
            }
        }

        // 攻击转向：在 OnAnimatorMove 里转，才能盖过同一帧的 Clip 根旋转
        public void SetSteerYaw(Vector3 worldDir, float degreesPerSecond)
        {
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

        // OnAnimatorMove 的旋转段：按优先级决定本帧朝向（原逻辑逐行迁移，调用时机不变）。
        // 位置增量（Root 位移）仍由 CharacterBody.OnAnimatorMove 先行写入，这里只管旋转。
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
