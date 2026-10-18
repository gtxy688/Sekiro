using System;
using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
namespace ARPG.FrameWork.Body
{

    // 记录 Boss 连续格挡和被完美弹反次数，达到连挡阈值后请求升级为完美弹反。
    // 是否能防御通过角色标志判断，状态切换交给构造时注入的委托。
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

        // ===== Boss 被动防御（只狼攻防转换）=====
        private int passiveDeflectCount;
        private float lastPassiveDeflectTime;

        // 可防御时按连续格挡次数选择普通格挡或完美弹反，并当场处理命中。
        public bool TryPassiveDeflect(HitData hit)
        {
            if (!body.EnablePassiveDeflect || body.IsDefeated) return false;
            if (hit.isPerilous || hit.attacker == null) return false;
            // 攻击中（可被抓前摇）/ 被弹硬直 / 崩解中 → 不回防御，走常规受击
            if (body.IsAttacking || body.IsParried || body.IsPostureBroken) return false;

            float now = Time.time;
            CharacterConfig config = body.Config;
            int threshold = config.PassiveDeflectThreshold;
            float resetWindow = config.PassiveDeflectResetWindow;
            if (passiveDeflectCount > 0 && now - lastPassiveDeflectTime > resetWindow)
                passiveDeflectCount = 0;
            lastPassiveDeflectTime = now;

            bool upgraded = passiveDeflectCount >= threshold;
            passiveDeflectCount = upgraded ? 0 : passiveDeflectCount + 1;

            // 先记连挡再请求切状态，因此空中等切换失败的命中也会消耗本次连挡计数。
            DeflectEntryMode mode = upgraded ? DeflectEntryMode.PerfectParry : DeflectEntryMode.PassiveGuard;
            return enterDeflectState(hit, mode);
        }
    }

}
