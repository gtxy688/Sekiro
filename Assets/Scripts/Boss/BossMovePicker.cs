using UnityEngine;
using System.Collections.Generic;

using ARPG.Boss.BehaviourTree;
using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
namespace ARPG.Boss
{

    // 按层加权抽招。缺 Animator 状态、冷却中、距离/额外条件不满足 → 权重 0。
    public static class BossMovePicker
    {
        // 对手与招式白名单由 Boss 实例传入，避免依赖全局角色或修改共享招式表；空白名单不过滤。
        public static BossMoveEntry Pick(
            BossMoveTable table,
            BossMoveLayer layer,
            CharacterBody self,
            Animator animator,
            Blackboard blackboard,
            float distance,
            CharacterBody opponent = null,
            HashSet<string> whitelist = null)
        {
            if (table.moves == null) return null;

            float postureLow = table.postureLowThreshold > 0f
                ? table.postureLowThreshold
                : self.Config.MaxPosture * 0.5f;

            // 第一遍累计有效权重，第二遍沿同一候选顺序定位随机落点。
            float total = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                float cursor = 0f;
                float roll = pass == 1 ? Random.Range(0f, total) : 0f;
                for (int i = 0; i < table.moves.Length; i++)
                {
                    BossMoveEntry e = table.moves[i];
                    if (whitelist != null && whitelist.Count > 0 && !whitelist.Contains(e.id)) continue;
                    float w = Weight(e, layer, self, opponent, animator, blackboard, distance, postureLow);
                    if (w <= 0f) continue;
                    if (pass == 0)
                    {
                        total += w;
                    }
                    else
                    {
                        cursor += w;
                        if (roll <= cursor) return table.moves[i];
                    }
                }
                if (pass == 0 && total <= 0f) return null;
            }

            return null;
        }

        // 不满足层、冷却、动画或额外条件时返回零；只有主动层按条目距离过滤。
        public static float Weight(
            BossMoveEntry e,
            BossMoveLayer layer,
            CharacterBody self,
            CharacterBody opponent,
            Animator animator,
            Blackboard blackboard,
            float distance,
            float postureLow)
        {
            if (e == null || e.layer != layer || e.weight <= 0f) return 0f;
            if (layer == BossMoveLayer.Active)
            {
                if (distance < e.minRange || distance > e.maxRange) return 0f;
            }

            if (blackboard.IsOnCooldown(e.id, e.cooldown)) return 0f;
            if (!AnySequencePlayable(e, animator)) return 0f;

            switch (e.extra)
            {
                case BossMoveExtra.HpBelow75:
                    if (self.CurrentHP >= self.Config.MaxHP * 0.75f)
                        return 0f;
                    break;
                case BossMoveExtra.PostureLow:
                    // PostureLow 是历史命名；实际要求累计架势达到阈值才放行。
                    if (self.CurrentPosture < postureLow)
                        return 0f;
                    break;
                case BossMoveExtra.ConsecutiveParry2:
                {
                    // 贴身交锋过多才跳：连续被对手完美弹开未满 2 次，权重为 0。
                    if (opponent != null && opponent.IsKnockedDown)
                        return 0f;
                    if (self.ConsecutiveTimesParried < 2)
                        return 0f;
                    break;
                }
                case BossMoveExtra.PlayerKnockedDown:
                {
                    if (opponent == null || !opponent.IsKnockedDown)
                        return 0f;
                    break;
                }
            }

            return e.weight;
        }

        public static bool AnySequencePlayable(BossMoveEntry e, Animator animator)
        {
            if (e == null || e.sequences == null) return false;
            for (int i = 0; i < e.sequences.Length; i++)
            {
                if (SequencePlayable(e.sequences[i], animator)) return true;
            }
            return false;
        }

        public static bool SequencePlayable(BossAnimSequence seq, Animator animator)
        {
            if (seq == null || seq.states == null || seq.states.Length == 0) return false;
            for (int i = 0; i < seq.states.Length; i++)
            {
                if (string.IsNullOrEmpty(seq.states[i])) return false;
                if (!AnimUtil.HasState(animator, seq.states[i])) return false;
            }
            return true;
        }

        public static BossAnimSequence ChooseSequence(BossMoveEntry e, Animator animator)
        {
            return ChooseSequence(e, animator, null, null);
        }

        public static BossAnimSequence ChooseSequence(
            BossMoveEntry e, Animator animator, CharacterBody self, BossMoveTable table)
        {
            // 只在整套动画均存在的分支中等概率选择。
            return ChooseUniformSequence(e, animator);
        }

        static BossAnimSequence ChooseUniformSequence(BossMoveEntry e, Animator animator)
        {
            if (e == null || e.sequences == null) return null;
            int playable = 0;
            for (int i = 0; i < e.sequences.Length; i++)
            {
                if (SequencePlayable(e.sequences[i], animator)) playable++;
            }

            if (playable <= 0) return null;

            int pick = Random.Range(0, playable);
            for (int i = 0; i < e.sequences.Length; i++)
            {
                if (!SequencePlayable(e.sequences[i], animator)) continue;
                if (pick == 0) return e.sequences[i];
                pick--;
            }

            return null;
        }

        public static BossMoveWindow WindowFor(BossMoveEntry e, int segmentIndex)
        {
            return WindowFor(e, segmentIndex, null);
        }

        // 分支自带窗口优先，否则使用招式共用窗口；段数超出时沿用最后一个窗口。
        public static BossMoveWindow WindowFor(BossMoveEntry e, int segmentIndex, BossAnimSequence seq)
        {
            if (seq != null && seq.windows != null && seq.windows.Length > 0)
            {
                int i = Mathf.Clamp(segmentIndex, 0, seq.windows.Length - 1);
                return seq.windows[i];
            }

            if (e == null || e.windows == null || e.windows.Length == 0)
                return new BossMoveWindow();
            int iEntry = Mathf.Clamp(segmentIndex, 0, e.windows.Length - 1);
            return e.windows[iEntry];
        }
    }

}
