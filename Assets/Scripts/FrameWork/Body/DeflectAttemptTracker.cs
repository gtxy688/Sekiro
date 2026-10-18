using UnityEngine;

namespace ARPG.FrameWork.Body
{
    // 显式区分主动离开防御与生命周期取消，决定是否记一次空按。
    public enum DeflectAttemptEndReason
    {
        Expired,      // 窗口自然到期仍未弹中 → 空按一次
        Replaced,     // 被新的有效按下提前替换 → 空按一次
        LeftDefense,  // 垫步等主动离开防御 → 空按一次
        HitFailed,    // 窗口内遇到无法弹反的命中被迫受击 → 空按一次
        Lifecycle,    // 死亡 / 复战 / 对象停用 / 输入模式切换 → 取消，不记空按
    }

    // 按真实按下身份管理弹反窗口与空按惩罚，成功和结束均只结算一次。
    // 时间与配置由 CharacterBody 传入，本类不操作输入、动画或状态机。
    public class DeflectAttemptTracker
    {
        // 参数非法时使用的安全值。
        public const float FallbackWindow = 0.5f;
        public const float FallbackWindowMin = 0.1f;
        public const float FallbackResetInterval = 0.5f;

        // 窗口与空按惩罚参数快照。来源 CharacterConfig，由 CharacterBody 组装后传入。
        public struct Settings
        {
            public float Window;              // 基础完美弹反窗口（秒）
            public int EmptyGraceCount;       // 前 N 次连续空按不缩窗
            public float EmptyPenalty;        // 超出宽容次数后，每个空按的累乘系数
            public float WindowMin;           // 窗口下限（秒）
            public float EmptyResetInterval;  // 相邻两次实际开启尝试的间隔超过该值则恢复

            // 将窗口、惩罚系数和恢复间隔限制在有效范围，避免窗口失效；corrected 供调用方告警。
            public Settings Normalized(out bool corrected)
            {
                corrected = false;
                Settings s = this;

                if (s.Window <= 0f) { s.Window = FallbackWindow; corrected = true; }
                if (s.WindowMin <= 0f) { s.WindowMin = FallbackWindowMin; corrected = true; }
                if (s.WindowMin > s.Window) { s.WindowMin = s.Window; corrected = true; }
                if (s.EmptyPenalty <= 0f || s.EmptyPenalty > 1f) { s.EmptyPenalty = 1f; corrected = true; }
                if (s.EmptyGraceCount < 0) { s.EmptyGraceCount = 0; corrected = true; }
                if (s.EmptyResetInterval <= 0f) { s.EmptyResetInterval = FallbackResetInterval; corrected = true; }

                return s;
            }
        }

        // ===== 输入边沿身份 =====
        // 只有真实按下生成 ID，系统回防不开主动窗口；ID 不复用，以便识别复位前的残留命令。
        private int nextPressId = 1;
        private int lastPressId;
        private int lastReleasedPressId;
        private int attemptedFloor;          // 复位/清输入时冻结：小于它的 PressId 一律视为残留

        private int lastAttemptedPressId;    // 同一次按下只开一次窗口（缓冲重试 / 状态重入）

        // ===== 当前主动尝试 =====
        private int nextAttemptId = 1;
        private int activeAttemptId;
        private int activePressId;
        private float activeStartTime;
        private float activeDeadline;
        private float activeWindow;
        private bool activeSucceeded;

        // ===== 空按记账 =====
        private int emptyCount;
        private float lastBeginTime;

        public int EmptyCount => emptyCount;
        public int ActiveAttemptId => activeAttemptId;
        public bool HasActiveAttempt => activeAttemptId != 0;
        public float ActiveWindow => activeWindow;
        public float ActiveStartTime => activeStartTime;
        public float ActiveDeadline => activeDeadline;
        public int LastPressId => lastPressId;

        // 防御键当前是否按住。物理按下才算按住：松开后即使还有残留命令也不成立。
        public bool IsDeflectHeld => lastPressId > 0 && lastPressId > lastReleasedPressId;

        // ===== 输入边沿身份 =====

        // 真实物理按下：生成新的按下身份。
        public int BeginPress()
        {
            lastPressId = nextPressId++;
            return lastPressId;
        }

        // 松开：记录"最高已释放的 PressId"。延迟处理旧释放时不会关掉之后的新按下。
        public void ReleasePress()
        {
            if (lastPressId <= 0) return;
            if (lastPressId > lastReleasedPressId) lastReleasedPressId = lastPressId;
        }

        public bool IsPressReleased(int pressId)
        {
            return pressId > 0 && pressId <= lastReleasedPressId;
        }

        // ===== 主动尝试 =====

        // 开启主动尝试并返回 ID；系统回防、重复按下或复位残留返回 0。
        // 先结算旧尝试，再恢复计数并计算新窗口，确保缩窗只依据已经发生的空按。
        public int BeginAttempt(int pressId, float now, Settings settings)
        {
            if (pressId <= 0)
            {
                return 0;
            }

            if (pressId <= lastAttemptedPressId || pressId < attemptedFloor)
            {
                return 0;
            }

            Settings s = settings.Normalized(out _);

            // 1. 旧尝试：还没到期就是被新按下替换，已经到期就是自然到期 —— 两者都只记一次空按。
            EndActiveAttempt(now < activeDeadline
                ? DeflectAttemptEndReason.Replaced
                : DeflectAttemptEndReason.Expired);

            // 2. 恢复：按"相邻两次实际开启尝试的间隔"判断，不按结算时刻判断。
            //    否则缓慢、正常的点按也会被当成连续空按。
            bool reset = lastBeginTime > 0f && now - lastBeginTime > s.EmptyResetInterval;
            if (reset) emptyCount = 0;

            // 3. 新窗口按当前空按次数计算：penaltySteps = max(0, emptyCount - grace)
            float window = ComputeWindow(emptyCount, s);
            int attemptId = nextAttemptId++;

            activeAttemptId = attemptId;
            activePressId = pressId;
            activeStartTime = now;
            activeWindow = window;
            activeDeadline = now + window;
            activeSucceeded = false;
            lastAttemptedPressId = pressId;
            lastBeginTime = now;

            return attemptId;
        }

        // 下一次窗口：max(最小窗口, 基础窗口 × 惩罚系数^超出宽容次数)。
        public static float ComputeWindow(int emptyCount, Settings settings)
        {
            Settings s = settings.Normalized(out _);
            int penaltySteps = emptyCount - s.EmptyGraceCount;
            if (penaltySteps <= 0) return s.Window;

            float window = s.Window * Mathf.Pow(s.EmptyPenalty, penaltySteps);
            return window < s.WindowMin ? s.WindowMin : window;
        }

        // 到期推进：状态 Update 和命中入口都要先调用它，否则两者的执行顺序会改变"窗口外命中"的结果。
        // 一次尝试只结算一次。
        public void Advance(float now)
        {
            if (activeAttemptId == 0) return;
            if (now < activeDeadline) return;
            EndActiveAttempt(DeflectAttemptEndReason.Expired);
        }

        // 资格只看当前尝试的半开区间 [start, deadline)，截止时刻不算成功，抬刀动画不延长窗口。
        public bool CanParry(int attemptId, float now)
        {
            if (attemptId <= 0 || attemptId != activeAttemptId) return false;
            return activeStartTime <= now && now < activeDeadline;
        }

        // 成功记账：只有当前有效尝试能清罚，且不刷新起点、不延长截止时间、不扩大当前窗口。
        // 一次尝试重复成功只认第一次；已成功的尝试之后结束时不再记空按。
        public bool RecordSuccess(int attemptId, float now)
        {
            if (!CanParry(attemptId, now)) return false;
            if (activeSucceeded) return false;

            activeSucceeded = true;
            emptyCount = 0;
            return true;
        }

        // 按状态机给出的原因结束尝试，非当前尝试或重复结束不再记账。
        public bool EndAttempt(int attemptId, DeflectAttemptEndReason reason)
        {
            if (attemptId <= 0 || attemptId != activeAttemptId) return false;
            EndActiveAttempt(reason);
            return true;
        }

        // 复战 / 死亡：取消未完成尝试（不记空按），清除空按计数与输入身份。
        public void Reset()
        {
            EndActiveAttempt(DeflectAttemptEndReason.Lifecycle);
            emptyCount = 0;
            lastBeginTime = 0f;
            ClearInput();
        }

        // 清除尝试和输入身份，保留空按记忆；恢复后只接受新的按下，复位前的命令按过期处理。
        public void ClearInput()
        {
            EndActiveAttempt(DeflectAttemptEndReason.Lifecycle);
            attemptedFloor = nextPressId;
            lastAttemptedPressId = 0;
            lastPressId = 0;
            lastReleasedPressId = 0;
        }

        // 结束当前尝试：成功过的尝试不再记空按；生命周期取消不记空按。
        private void EndActiveAttempt(DeflectAttemptEndReason reason)
        {
            if (activeAttemptId == 0) return;

            bool penalize = !activeSucceeded && reason != DeflectAttemptEndReason.Lifecycle;
            if (penalize) emptyCount++;

            activeAttemptId = 0;
            activePressId = 0;
            activeStartTime = 0f;
            activeDeadline = 0f;
            activeWindow = 0f;
            activeSucceeded = false;

        }
    }
}
