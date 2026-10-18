using UnityEngine;

using ARPG.FrameWork;
namespace ARPG.FrameWork.Body
{

    // 管理接地检测、坠图保护、跳跃速度、移动动画参数和相机相对输入换算。
    // 配置与移动意图读取 CharacterBody，内部缓存感知结果、待补写的跳跃速度与骨骼引用。
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

            if (body.Animator.isHuman)
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
            bool check = Physics.CheckSphere(body.groundCheckPoint.position, body.groundCheckRadius, body.groundLayer);
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

            // 新结果连续保持 N 帧才翻转接地状态，避免地面边缘抖动导致反复切换空地状态。
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
        // 安全点只认原始检测结果，避免接地迟滞把刚离地的空中位置误记为落点。
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
            body.Rb.position = lastSafePosition;
            body.Rb.velocity = Vector3.zero;
            Debug.LogWarning($"[CharacterBody] {body.name} 坠出地图（y < {body.fallKillY}），已传回安全落点 {lastSafePosition}");
        }

        // 起跳后在 FixedUpdate 补写一次速度，避免 Animate Physics 的根运动覆盖起跳速度。
        private float pendingJumpSpeed;
        private bool hasPendingJump;

        // 起跳：只打垂直初速度。根运动保持开着，由 OnAnimatorMove 丢掉 Y、保留 XZ。
        public void QueueJump()
        {
            if (body.IsFinisherLocked) return;

            float speed = body.Config.JumpSpeed;

            pendingJumpSpeed = speed;
            hasPendingJump = true;
            ApplyJumpVelocity(speed);
        }

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

            AnimUtil.TryCrossFade(body.Animator, "Jump2", body.Config.JumpAnimBlend);
            playedNew = true;
            return true;
        }

        private void ApplyJumpVelocity(float speed)
        {
            Vector3 v = body.Rb.velocity;
            body.Rb.velocity = new Vector3(v.x, speed, v.z);
        }

        // CharacterBody.FixedUpdate 只在有待处理起跳时补写速度。
        public void ApplyPendingJumpVelocity()
        {
            if (!hasPendingJump) return;
            ApplyJumpVelocity(pendingJumpSpeed);
            hasPendingJump = false;
        }

        // 忍杀锁定时取消下一物理帧的起跳补写。
        public void CancelPendingJump()
        {
            hasPendingJump = false;
        }

        // 按相机水平前方和右方，将摇杆输入转换为世界方向。
        public Vector3 InputToWorldDir(Vector2 inputDir)
        {
            UnityEngine.Camera cam = body.GameCamera != null ? body.GameCamera : UnityEngine.Camera.main;
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            return (camForward * inputDir.y + camRight * inputDir.x).normalized;
        }

        // 玩家 Controller 用 MoveZ，Boss 用 MoveY。缓存起来避免每帧 SetFloat 打到不存在的参数。
        private int moveXHash;
        private int moveForwardHash;
        private bool moveParamsResolved;

        // 锁定四向移动参数：有 MoveZ 用 MoveZ，否则回退 MoveY。
        public void SetMoveStrafe(float x, float z, bool instant)
        {
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
            if (moveParamsResolved) return;
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
