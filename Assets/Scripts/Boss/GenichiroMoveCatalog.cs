using UnityEngine;

// 弦一郎默认招式表。由 Editor 按钮写入 BossMoveTable，不要运行时调用。
public static class GenichiroMoveCatalog
{
    static BossMoveWindow Hit(float duration, float hitAt = 0.2f, float recoverAt = -1f,
        PerilousType perilous = PerilousType.None,
        AttackHitboxSlot slot = AttackHitboxSlot.Weapon)
    {
        float rec = recoverAt > 0f ? recoverAt : duration * 0.55f;
        return new BossMoveWindow
        {
            hitStartTime = hitAt,
            recoverStart = rec,
            comboWindowEnd = Mathf.Min(duration, rec + 0.15f),
            stateDuration = duration,
            rotateEnd = Mathf.Min(0.35f, duration),
            transitionDuration = 0.1f,
            perilous = perilous,
            hitboxSlot = slot
        };
    }

    // startEnd：start0,end0,start1,end1… 每对是一刀。关刀会清 hitTargets，下一对才是新的一次伤害。
    static BossMoveWindow Hits(float duration, params float[] startEnd)
    {
        if (startEnd == null || startEnd.Length < 2 || (startEnd.Length & 1) != 0)
            return Hit(duration);

        HitPulse[] pulses = new HitPulse[startEnd.Length / 2];
        for (int i = 0; i < pulses.Length; i++)
        {
            pulses[i] = new HitPulse
            {
                start = startEnd[i * 2],
                end = startEnd[i * 2 + 1]
            };
        }

        float lastEnd = pulses[pulses.Length - 1].end;
        return new BossMoveWindow
        {
            hitStartTime = pulses[0].start,
            recoverStart = lastEnd,
            comboWindowEnd = Mathf.Min(duration, lastEnd + 0.15f),
            stateDuration = duration,
            rotateEnd = Mathf.Min(0.35f, duration),
            transitionDuration = 0.1f,
            hitPulses = pulses
        };
    }

    static BossMoveWindow NoHit(float duration)
    {
        return new BossMoveWindow
        {
            hitStartTime = duration,
            recoverStart = duration,
            comboWindowEnd = duration,
            stateDuration = duration,
            rotateEnd = Mathf.Min(0.15f, duration),
            transitionDuration = 0.08f
        };
    }

    static BossAnimSequence Seq(params string[] states)
    {
        return new BossAnimSequence { states = states };
    }

    static BossMoveEntry Move(
        string id, BossMoveLayer layer,
        float min, float max, float weight, float cooldown,
        BossAnimSequence[] sequences, BossMoveWindow[] windows,
        PerilousType perilous = PerilousType.None,
        BossMoveExtra extra = BossMoveExtra.None,
        int dmg = 10, float posture = 15f)
    {
        return new BossMoveEntry
        {
            id = id,
            layer = layer,
            minRange = min,
            maxRange = max,
            weight = weight,
            cooldown = cooldown,
            sequences = sequences,
            windows = windows,
            perilous = perilous,
            extra = extra,
            baseDamage = dmg,
            postureDamage = posture
        };
    }

    public static void Apply(BossMoveTable t)
    {
        t.kengekiMaxRange = 2.5f;
        t.postureLowThreshold = 0f;
        t.air5HeavyInterruptChance = 0.5f;
        t.moves = new[]
        {
            Move("Bow_ThenSlash", BossMoveLayer.Active, 7f, 99f, 600f, 6f,
                new[] { Seq("Bow_Shot", "3015") }, new[] { Hit(1.6f), Hit(1.8f) }),
            Move("Bow_Shot", BossMoveLayer.Active, 7f, 99f, 200f, 5f,
                new[] { Seq("Bow_Shot") }, new[] { Hit(1.6f) }),
            Move("Slash_Rush2", BossMoveLayer.Active, 5f, 99f, 300f, 6f,
                new[] { Seq("Slash_Rush2") }, new[] { Hit(2.2f) }),
            Move("Slash_RushThenBow", BossMoveLayer.Active, 5f, 7f, 100f, 8f,
                new[] { Seq("Kengeki_Heavy", "3011") }, new[] { Hit(1.8f), Hit(1.6f) }),
            Move("Boat", BossMoveLayer.Active, 3f, 7f, 300f, 10f,
                new[] { Seq("Boat1", "Boat2") },
                new[]
                {
                    // Boat1 一条动画 5 段出伤；时间按挥刀帧再对。
                    Hits(2.4f,
                        0.20f, 0.32f,
                        0.48f, 0.60f,
                        0.76f, 0.88f,
                        1.04f, 1.16f,
                        1.32f, 1.44f),
                    Hit(2.4f)
                },
                extra: BossMoveExtra.PostureLow),
            Move("Slash_Double", BossMoveLayer.Active, 3f, 5f, 10f, 4f,
                new[] { Seq("Slash_Double") }, new[] { Hit(1.8f) }),
            Move("Slash_Heavy", BossMoveLayer.Active, 3f, 5f, 30f, 5f,
                new[] { Seq("Slash_Heavy") }, new[] { Hit(2.0f) }),
            Move("Slash_SpinElbow", BossMoveLayer.Active, 0f, 5f, 15f, 6f,
                new[] { Seq("Slash_Spin", "Elbow") },
                new[] { Hit(1.6f), Hit(1.4f, perilous: PerilousType.Grab, slot: AttackHitboxSlot.Elbow) }),
            Move("Slash_StepTurn", BossMoveLayer.Active, 0f, 3f, 15f, 4f,
                new[] { Seq("Slash_StepTurn") }, new[] { Hit(1.8f) }),
            Move("Kick", BossMoveLayer.Active, 0f, 3f, 30f, 5f,
                new[] { Seq("Attack_Slash", "Kick") }, new[] { Hit(1.2f), Hit(1.4f) }),
            Move("JumpThrust", BossMoveLayer.Active, 0f, 5f, 20f, 8f,
                new[] { Seq("JumpThrust") }, new[] { Hit(2.2f, 0.35f, 1.4f) },
                PerilousType.JumpThrust),
            Move("Perilous_Sweep", BossMoveLayer.Active, 0f, 5f, 10f, 8f,
                new[] { Seq("Sweep") }, new[] { Hit(2.4f, 0.4f, 1.6f) },
                PerilousType.Sweep),
            Move("Bow_Air5", BossMoveLayer.Active, 0f, 3f, 30f, 10f,
                new[] { Seq("Dodge_Back", "Bow_Air5") }, new[] { NoHit(0.55f), Hit(4.5f, 0.3f, 4.0f) }),

            Move("Bow_Heavy", BossMoveLayer.Interrupt, 0f, 99f, 1f, 8f,
                new[] { Seq("Bow_Heavy") }, new[] { Hit(3.0f, 0.4f, 2.2f) }, dmg: 25, posture: 30f),

            Move("Kengeki_Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 40f, 0.5f,
                new[] { Seq("3050"), Seq("3055"), Seq("3065"), Seq("3071"), Seq("3076") },
                new[] { Hit(1.5f) }),
            Move("Kengeki_Double", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 3f,
                new[] { Seq("Kengeki_Double") }, new[] { Hit(1.8f) }),
            Move("Kengeki_Thrust", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
                new[] { Seq("Kengeki_Thrust") }, new[] { Hit(2.0f, 0.35f, 1.3f) },
                PerilousType.Thrust),
            Move("Kengeki_Heavy", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
                new[] { Seq("Step_L", "Kengeki_Heavy"), Seq("Step_R", "Kengeki_Heavy") },
                new[] { NoHit(0.45f), Hit(1.8f) }),
            Move("Kengeki_Bow", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 5f,
                new[] { Seq("3031", "3019", "3029"), Seq("3031", "3036") },
                new[] { NoHit(0.8f), Hit(1.8f), Hit(1.8f) }),
            Move("Kengeki_Air5", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 8f,
                new[] { Seq("Dodge_Back", "Bow_Air5") }, new[] { NoHit(0.55f), Hit(4.5f, 0.3f, 4.0f) }),
            Move("Boat_Full", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 12f,
                new[] { Seq("Boat_Full") }, new[] { Hit(3.2f) },
                extra: BossMoveExtra.HpBelow75),
            Move("Kengeki_Bow2Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("3018", "3015") }, new[] { Hit(1.6f), Hit(1.8f) }),
            Move("Kengeki_JumpBow", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("3034", "3036", "3015") }, new[] { Hit(1.6f), Hit(1.6f), Hit(1.8f) }),
            Move("Bow_AirHeavy", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("Bow_AirHeavy") }, new[] { Hit(2.2f) })
        };
    }
}
