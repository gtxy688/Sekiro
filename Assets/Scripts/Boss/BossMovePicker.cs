using UnityEngine;

// 按层加权抽招。缺 Animator 状态、冷却中、距离/额外条件不满足 → 权重 0。
public static class BossMovePicker
{
    public static BossMoveEntry Pick(
        BossMoveTable table,
        BossMoveLayer layer,
        CharacterBody self,
        Animator animator,
        Blackboard blackboard,
        float distance)
    {
        if (table == null || table.moves == null || self == null) return null;

        float postureLow = table.postureLowThreshold > 0f
            ? table.postureLowThreshold
            : (self.Config != null ? self.Config.MaxPosture * 0.5f : 50f);

        float total = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            float cursor = 0f;
            float roll = pass == 1 ? Random.Range(0f, total) : 0f;
            for (int i = 0; i < table.moves.Length; i++)
            {
                BossMoveEntry e = table.moves[i];
                // 运行时白名单（MeleeOnly 调试）：非空时只放行名单内招式
                if (table.moveWhitelist != null && table.moveWhitelist.Count > 0
                    && !table.moveWhitelist.Contains(e.id)) continue;
                float w = Weight(e, layer, self, animator, blackboard, distance, postureLow);
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

    public static float Weight(
        BossMoveEntry e,
        BossMoveLayer layer,
        CharacterBody self,
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

        if (blackboard != null && blackboard.IsOnCooldown(e.id, e.cooldown)) return 0f;
        if (!AnySequencePlayable(e, animator)) return 0f;

        switch (e.extra)
        {
            case BossMoveExtra.HpBelow75:
                if (self.Config == null || self.CurrentHP >= self.Config.MaxHP * 0.75f)
                    return 0f;
                break;
            case BossMoveExtra.PostureLow:
                // CurrentPosture 是累计值：0=架势空，Max=崩。
                // 累计已过阈值 = 架势偏高（濒临崩解）→ 放行（人设已确认：架势越高越容易放飞舟）。
                if (self.CurrentPosture < postureLow)
                    return 0f;
                break;
            case BossMoveExtra.ConsecutiveParry2:
                // 贴身交锋过多才跳：连续被玩家完美弹开未满 2 次，权重为 0。
                if (self.ConsecutiveTimesParried < 2)
                    return 0f;
                break;
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
            if (animator != null && !AnimUtil.HasState(animator, seq.states[i])) return false;
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
        if (e != null && e.id == "JumpThrust")
            return ChooseJumpThrustSequence(e, animator, self, table);
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

    // 第一条命只突刺；第二条命横扫:突刺 = 表上权重（默认 7:3）。
    static BossAnimSequence ChooseJumpThrustSequence(
        BossMoveEntry e, Animator animator, CharacterBody self, BossMoveTable table)
    {
        if (e == null || e.sequences == null) return null;

        bool firstLife = self == null || self.Config == null
            || (self.Config.LifeCount - self.LivesRemaining) <= 0;
        float sweepW = 0f;
        float thrustW = 1f;
        if (!firstLife)
        {
            sweepW = table != null ? table.jumpThrustLife2SweepWeight : 7f;
            thrustW = table != null ? table.jumpThrustLife2ThrustWeight : 3f;
            if (sweepW < 0f) sweepW = 0f;
            if (thrustW < 0f) thrustW = 0f;
        }

        float sweepTotal = 0f;
        float thrustTotal = 0f;
        int sweepCount = 0;
        int thrustCount = 0;
        for (int i = 0; i < e.sequences.Length; i++)
        {
            if (!SequencePlayable(e.sequences[i], animator)) continue;
            if (IsSweepLanding(e.sequences[i]))
            {
                sweepCount++;
                sweepTotal = sweepW;
            }
            else
            {
                thrustCount++;
                thrustTotal = thrustW;
            }
        }

        float total = 0f;
        if (sweepCount > 0) total += sweepTotal;
        if (thrustCount > 0) total += thrustTotal;
        if (total <= 0f)
            return ChooseUniformSequence(e, animator);

        bool pickSweep = sweepCount > 0 && Random.Range(0f, total) < sweepTotal;
        int remain = pickSweep ? Random.Range(0, sweepCount) : Random.Range(0, thrustCount);
        for (int i = 0; i < e.sequences.Length; i++)
        {
            if (!SequencePlayable(e.sequences[i], animator)) continue;
            bool sweep = IsSweepLanding(e.sequences[i]);
            if (sweep != pickSweep) continue;
            if (remain == 0) return e.sequences[i];
            remain--;
        }

        return ChooseUniformSequence(e, animator);
    }

    static bool IsSweepLanding(BossAnimSequence seq)
    {
        if (seq == null || seq.states == null || seq.states.Length == 0) return false;
        string last = seq.states[seq.states.Length - 1];
        return last == "Sweep";
    }

    public static BossMoveWindow WindowFor(BossMoveEntry e, int segmentIndex)
    {
        return WindowFor(e, segmentIndex, null);
    }

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
