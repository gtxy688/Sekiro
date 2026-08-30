using UnityEngine;

using ARPG.FrameWork;
namespace ARPG.FrameWork.Body
{

    // 移动感知模块（CharacterBody 重构）：接地检测（迟滞防抖）+ 坠图保护 + 跳跃冲量 +
    // 锁定四向动画参数 + 相机相对输入换算 + 跳跃跟髋骨采样。
    // 约束：
    // - Inspector 字段（groundCheckPoint/groundLayer/groundHysteresisFrames/fallKillY/GameCamera 等）
    //   留在 CharacterBody（prefab 已保存），模块只读；
    // - 接地接线 EnsureGroundDetectionWired 留在 Façade（Awake 里写序列化字段、依赖私有 bodyCollider）；
    // - MoveDirection/IsAirborne 等移动意图标志由状态/AI 写入，仍留在 Façade，模块不拥有；
    // - 跳跃冲量等 FixedUpdate 再写速度：Animator 是 Animate Physics，根运动会在物理帧盖掉
    //   velocity.y，所以起跳只记 pending（ApplyPendingJumpVelocity 由 Façade 的 FixedUpdate 驱动，时机不变）。
    public class Locomotion
    {
        private readonly CharacterBody body;

        public Locomotion(CharacterBody body)
        {
            this.body = body;
        }

        public bool IsGrounded { get; private set; }
        public bool AirJump2Used { get; private set; }

        public void ResetAirJump2()
        {
            AirJump2Used = false;
        }

        // 跳跃镜头跟髋骨：keepOriginalPositionY 时根可能贴地，视觉却在天上。
        // GetBoneTransform 只认 Humanoid；弦一郎是 Generic（髋骨名 Pelvis），直接调会抛 InvalidOperationException。
        private Transform jumpFollowBone;
        private bool jumpFollowBoneResolved;

        public float GetJumpFollowWorldY()
        {
            Transform bone = ResolveJumpFollowBone();
            return bone != null ? bone.position.y : body.transform.position.y;
        }

        Transform ResolveJumpFollowBone()
        {
            if (jumpFollowBoneResolved) return jumpFollowBone;
            jumpFollowBoneResolved = true;

            if (body.Animator != null && body.Animator.isHuman)
                jumpFollowBone = body.Animator.GetBoneTransform(HumanBodyBones.Hips);

            if (jumpFollowBone == null)
                jumpFollowBone = FindNamedChild(body.transform, "Pelvis")
                    ?? FindNamedChild(body.transform, "Hips")
                    ?? FindNamedChild(body.transform, "Hip")
                    ?? FindNamedChild(body.transform, "Spine");

            return jumpFollowBone;
        }

        static Transform FindNamedChild(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamedChild(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        // --- 物理环境检测 ---
        private bool groundedHysteresis;     // 上一帧接地结果
        private int groundedChangeFrames;    // 连续"与上一帧相反"的帧数
        private bool groundedCheckRaw;       // 本帧 CheckSphere 原始结果（未迟滞，坠图保护用）
        private bool groundedCheckPrimed;    // 第一次检测直接采信，避免默认 false 让进场播 Fall

        public void UpdateEnvironmentalChecks()
        {
            bool check;
            if (body.groundCheckPoint != null)
            {
                check = Physics.CheckSphere(body.groundCheckPoint.position, body.groundCheckRadius, body.groundLayer);
            }
            else
            {
                check = true; // 容错
            }
            groundedCheckRaw = check;

            // 第一次没有「上一帧」：直接采信，不要从默认 false 再等迟滞。
            if (!groundedCheckPrimed)
            {
                groundedCheckPrimed = true;
                groundedHysteresis = check;
                groundedChangeFrames = 0;
                IsGrounded = check;
                return;
            }

            // 迟滞防抖：结果必须连续 N 帧保持一致才翻转 IsGrounded。
            // 否则球边缘蹭到地面时，物理步进会让 true/false 每帧抖动，
            // 导致 GroundedState(Idle) ↔ AirState(Jump) 反复横跳（"莫名其妙的待机+跳跃动画"）
            if (check == groundedHysteresis)
            {
                groundedChangeFrames = 0;
            }
            else
            {
                groundedChangeFrames++;
                if (groundedChangeFrames >= body.groundHysteresisFrames)
                {
                    groundedHysteresis = check;
                    groundedChangeFrames = 0;
                }
            }
            IsGrounded = groundedHysteresis;
        }

        // --- 坠出地图保护 ---
        // 脚下有真实地面时持续记录；坠过 fallKillY 传回最近一次记录。
        // 必须用原始检测结果记录，不能用 IsGrounded：迟滞有 2 帧延迟，
        // 被传送/击飞到空中的头几帧 IsGrounded 仍是 true，会把空中坐标记成"安全点"，
        // 之后每次回传都落回虚空，永远回不到地面（实测踩过的坑）。
        // 写 transform 后同步 Rb.position 并清零速度（与 OnAnimatorMove 同一套写法），
        // 落回地面后原始检测立刻为 true，迟滞跟进翻转，空中状态经正常落地流程回地面。
        private Vector3 lastSafePosition;
        private bool hasSafePosition;

        public void UpdateFallSafety()
        {
            if (groundedCheckRaw)
            {
                lastSafePosition = body.transform.position;
                hasSafePosition = true;
                return;
            }

            if (!hasSafePosition || body.transform.position.y >= body.fallKillY) return;

            body.transform.position = lastSafePosition;
            if (body.Rb != null)
            {
                body.Rb.position = lastSafePosition;
                body.Rb.velocity = Vector3.zero;
            }
            Debug.LogWarning($"[CharacterBody] {body.name} 坠出地图（y < {body.fallKillY}），已传回安全落点 {lastSafePosition}");
        }

        // 跳跃冲量要等到 FixedUpdate 再写速度：
        // Animator 是 Animate Physics，根运动在物理帧里会把 velocity.y 盖掉。
        private float pendingJumpSpeed;
        private bool hasPendingJump;

        // 起跳：只打垂直初速度。根运动保持开着，由 OnAnimatorMove 丢掉 Y、保留 XZ。
        public void QueueJump()
        {
            if (body.IsFinisherLocked) return;

            float speed = body.Config != null ? body.Config.JumpSpeed : 6f;
            if (speed <= 0.01f) speed = 6f;

            pendingJumpSpeed = speed;
            hasPendingJump = true;
            ApplyJumpVelocity(speed);
        }

        // （横扫跳踩已删除：未实现对应踩头反制，Jump2 保留为空中二段）

        // 返回 true = 命令吃掉。playedNew = 本次新播了 Jump2（已用过则为 false）。
        public bool TryAirJump2(out bool playedNew)
        {
            playedNew = false;
            if (AirJump2Used) return true;
            AirJump2Used = true;

            if (!AnimUtil.HasState(body.Animator, "Jump2"))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少状态：Jump2");
                return true;
            }

            AnimUtil.TryCrossFade(body.Animator, "Jump2", body.Config != null ? body.Config.JumpAnimBlend : 0.08f);
            playedNew = true;
            return true;
        }

        private void ApplyJumpVelocity(float speed)
        {
            if (body.Rb == null) return;
            Vector3 v = body.Rb.velocity;
            body.Rb.velocity = new Vector3(v.x, speed, v.z);
        }

        // Façade 的 FixedUpdate 每物理帧调用：pending 存在才写速度
        public void ApplyPendingJumpVelocity()
        {
            if (!hasPendingJump || body.Rb == null) return;
            ApplyJumpVelocity(pendingJumpSpeed);
            hasPendingJump = false;
        }

        // 忍杀锁定时取消未生效的起跳（原 IsFinisherLocked setter 内联逻辑）
        public void CancelPendingJump()
        {
            hasPendingJump = false;
        }

        // 摇杆输入 → 世界移动方向（相机相对，原神式）：
        // 输入先经相机水平朝向变换，W = 远离镜头、A/D = 屏幕左右，与相机摆放无关
        public Vector3 InputToWorldDir(Vector2 inputDir)
        {
            UnityEngine.Camera cam = body.GameCamera != null ? body.GameCamera : UnityEngine.Camera.main;
            if (cam != null)
            {
                Vector3 camForward = cam.transform.forward;
                camForward.y = 0f;
                camForward.Normalize();
                Vector3 camRight = cam.transform.right;
                camRight.y = 0f;
                camRight.Normalize();
                return (camForward * inputDir.y + camRight * inputDir.x).normalized;
            }
            // 没有相机时回退世界方向（容错）
            return new Vector3(inputDir.x, 0f, inputDir.y).normalized;
        }

        // 玩家 Controller 用 MoveZ，Boss 用 MoveY。缓存起来避免每帧 SetFloat 打到不存在的参数。
        private int moveXHash;
        private int moveForwardHash;
        private bool moveParamsResolved;

        // 锁定四向移动参数：有 MoveZ 用 MoveZ，否则回退 MoveY。
        public void SetMoveStrafe(float x, float z, bool instant)
        {
            if (body.Animator == null) return;
            ResolveMoveParams();
            if (instant)
            {
                body.Animator.SetFloat(moveXHash, x);
                body.Animator.SetFloat(moveForwardHash, z);
            }
            else
            {
                body.Animator.SetFloat(moveXHash, x, 0.1f, Time.deltaTime);
                body.Animator.SetFloat(moveForwardHash, z, 0.1f, Time.deltaTime);
            }
        }

        private void ResolveMoveParams()
        {
            if (moveParamsResolved || body.Animator == null) return;
            // StringToHash 是 Animator 的静态方法，用类型名限定（不能经实例属性调用）
            moveXHash = Animator.StringToHash("MoveX");
            string forwardName = "MoveY";
            foreach (AnimatorControllerParameter parameter in body.Animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Float &&
                    parameter.name == "MoveZ")
                {
                    forwardName = "MoveZ";
                    break;
                }
            }
            moveForwardHash = Animator.StringToHash(forwardName);
            moveParamsResolved = true;
        }
    }

}
