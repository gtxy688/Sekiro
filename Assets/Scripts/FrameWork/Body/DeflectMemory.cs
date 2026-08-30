using System;
using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
namespace ARPG.FrameWork.Body
{

    // 弹反记忆模块（CharacterBody 重构试点二）。
    // 职责：玩家侧抖刀惩罚记账（M4）+ Boss 侧被动防御记账（M7：连挡计数升级完美弹反）+ 连续被弹计数。
    // 约束：
    // - 计数/时间戳/窗口缩放等运行时状态收在本类内部，CharacterBody 不再持有；
    // - Inspector 字段 EnablePassiveDeflect 留在 CharacterBody（prefab 已保存该值），模块只读；
    // - 切状态的编排权通过构造注入的委托交回 Façade：模块只判定"要不要进格挡、以哪种级别进"，
    //   自己不碰状态机（与"模块请求、Façade 编排"的拆分原则一致）；
    // - 架构红线：不判状态类型，只依赖 body 的语义化标志（IsAttacking/IsParried/IsPostureBroken/IsDefeated）。
    public class DeflectMemory
    {
        private readonly CharacterBody body;
        // 原地调 TryChangeGroundedSubState 并构造带 pendingHit 的 DeflectState（命中当场结算）。
        // 空中（AirState）等不可防御场景由该委托返回 false，交回常规受击链路。
        private readonly Func<HitData, DeflectEntryMode, bool> enterDeflectState;

        public DeflectMemory(CharacterBody body, Func<HitData, DeflectEntryMode, bool> enterDeflectState)
        {
            this.body = body;
            this.enterDeflectState = enterDeflectState;
        }

        // ===== 抖刀惩罚（M4）=====
        private int deflectMashCount;
        private float lastDeflectPressTime;
        private float deflectWindowScale = 1f;

        // DeflectState 进入时调用：0.5s 内连点 ≥3 次 → 窗口 ×0.75，下限 0.1s；停止 0.5s 后恢复
        public void RegisterDeflectPress()
        {
            CharacterConfig config = body.Config;
            float now = Time.time;
            if (now - lastDeflectPressTime > (config != null ? config.DeflectMashWindow : 0.5f))
            {
                deflectMashCount = 0;
                deflectWindowScale = 1f;
            }
            deflectMashCount++;
            lastDeflectPressTime = now;

            if (config != null && deflectMashCount > config.DeflectMashLimit)
            {
                deflectWindowScale *= config.DeflectMashPenalty;
                float min = config.DeflectWindowMin;
                float baseWindow = config.DeflectWindow;
                if (baseWindow * deflectWindowScale < min) deflectWindowScale = min / baseWindow;
            }
        }

        public float LastDeflectCancelTime { get; private set; }

        public void NotifyDeflectCancel()
        {
            LastDeflectCancelTime = Time.time;
        }

        // 当前生效的弹反窗口（已计入抖刀惩罚）
        public float GetDeflectWindow()
        {
            float baseWindow = body.Config != null ? body.Config.DeflectWindow : 0.3f;
            return baseWindow * deflectWindowScale;
        }

        // ===== 连续被弹计数 =====
        // 连续被对手近战完美弹开的次数。JumpThrust（3022）抽招读这个；出手或交锋中断后清零。
        public int ConsecutiveTimesParried { get; private set; }

        public void NotifyPerfectlyParried()
        {
            ConsecutiveTimesParried++;
        }

        public void ResetConsecutiveTimesParried()
        {
            ConsecutiveTimesParried = 0;
        }

        // ===== M7 Boss 被动防御（只狼攻防转换）=====
        private int passiveDeflectCount;
        private float lastPassiveDeflectTime;

        public bool TryPassiveDeflect(HitData hit)
        {
            if (!body.EnablePassiveDeflect || body.IsDefeated) return false;
            if (hit.isPerilous || hit.attacker == null) return false;
            // 攻击中（可被抓前摇）/ 被弹硬直 / 崩解中 → 不回防御，走常规受击
            if (body.IsAttacking || body.IsParried || body.IsPostureBroken) return false;

            float now = Time.time;
            CharacterConfig config = body.Config;
            int threshold = config != null ? config.PassiveDeflectThreshold : 2;   // 数值走 SO，缺失回退旧默认
            float resetWindow = config != null ? config.PassiveDeflectResetWindow : 2.5f;
            if (passiveDeflectCount > 0 && now - lastPassiveDeflectTime > resetWindow)
                passiveDeflectCount = 0;
            lastPassiveDeflectTime = now;

            bool upgraded = passiveDeflectCount >= threshold;
            passiveDeflectCount = upgraded ? 0 : passiveDeflectCount + 1;

            // 强制进格挡姿态并当场处理这次命中（PerfectParry 弹开攻击者 / PassiveGuard 普通格挡）。
            // 切状态编排权在 Façade（委托注入）。计数先行更新：即使进不了格挡（如空中），
            // 也和迁移前行为一致——本次命中仍消耗连挡窗口。
            DeflectEntryMode mode = upgraded ? DeflectEntryMode.PerfectParry : DeflectEntryMode.PassiveGuard;
            return enterDeflectState(hit, mode);
        }
    }

}
