using UnityEngine;

// 弦一郎默认招式表。由 Editor 按钮写入 BossMoveTable，不要运行时调用。
// 判定时间与当前已调好的 GenichiroMoveTable 对齐：误点「填入默认表」不会盖掉时间轴。
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
            hitboxSlot = slot,
            hitPulses = new HitPulse[0]
        };
    }

    // startEnd：start0,end0,start1,end1… 每对是一刀。comboEnd 按时间轴填写，不要用 lastEnd+0.15 猜。
    static BossMoveWindow Hits(float duration, float comboEnd, params float[] startEnd)
    {
        return Hits(duration, comboEnd, PerilousType.None, AttackHitboxSlot.Weapon, startEnd);
    }

    static BossMoveWindow Hits(
        float duration, float comboEnd,
        PerilousType perilous, AttackHitboxSlot slot,
        params float[] startEnd)
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
        float combo = comboEnd > 0f ? comboEnd : lastEnd;
        combo = Mathf.Clamp(combo, lastEnd, duration);
        return new BossMoveWindow
        {
            hitStartTime = pulses[0].start,
            recoverStart = lastEnd,
            comboWindowEnd = combo,
            stateDuration = duration,
            rotateEnd = Mathf.Min(0.35f, duration),
            transitionDuration = 0.1f,
            perilous = perilous,
            hitboxSlot = slot,
            hitPulses = pulses
        };
    }

    // 无近战判定。短垫步/起手（<=0.8s）转向窗更短，与时间轴一致。
    static BossMoveWindow NoHit(float duration)
    {
        bool shortMove = duration <= 0.8f;
        return new BossMoveWindow
        {
            hitStartTime = duration,
            recoverStart = duration,
            comboWindowEnd = duration,
            stateDuration = duration,
            rotateEnd = Mathf.Min(shortMove ? 0.15f : 0.35f, duration),
            transitionDuration = shortMove ? 0.08f : 0.1f,
            hitPulses = new HitPulse[0]
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
                new[] { Seq("Bow_Shot", "3015") },
                new[] { NoHit(1.6f), Hits(1.8f, 1.2035f, 1.082f, 1.2035f) }),
            Move("Bow_Shot", BossMoveLayer.Active, 7f, 99f, 200f, 5f,
                new[] { Seq("Bow_Shot") }, new[] { NoHit(1.6f) }),
            Move("Slash_Rush2", BossMoveLayer.Active, 5f, 99f, 300f, 6f,
                new[] { Seq("Slash_Rush2") },
                new[] { Hits(2.9333f, 1.36f, 0.75f, 0.9248f, 1.0552f, 1.2811f) }),
            Move("Slash_RushThenBow", BossMoveLayer.Active, 5f, 7f, 100f, 8f,
                new[] { Seq("Kengeki_Heavy", "3011") },
                new[] { Hits(1.852f, 1.852f, 1.2885f, 1.852f), NoHit(1.6f) }),
            Move("Boat", BossMoveLayer.Active, 3f, 7f, 300f, 10f,
                new[] { Seq("Boat1", "Boat2") },
                new[]
                {
                    Hits(3.3347f, 3.3347f,
                        1.9203f, 2.0419f,
                        2.3292f, 2.4387f,
                        2.8179f, 2.968f,
                        3.1545f, 3.3347f),
                    Hits(2.4f, 1.5151f,
                        0.191f, 0.3096f,
                        0.4892f, 0.5868f,
                        1.3637f, 1.5151f)
                },
                extra: BossMoveExtra.PostureLow),
            Move("Slash_Double", BossMoveLayer.Active, 3f, 5f, 10f, 4f,
                new[] { Seq("Slash_Double") },
                new[] { Hits(1.8f, 1.3194f, 0.8067f, 0.9394f, 1.2031f, 1.3194f) }),
            Move("Slash_Heavy", BossMoveLayer.Active, 3f, 5f, 30f, 5f,
                new[] { Seq("Slash_Heavy") },
                new[] { Hits(2f, 1.25f, 0.9875f, 1.1259f) }),
            Move("Slash_SpinElbow", BossMoveLayer.Active, 0f, 5f, 15f, 6f,
                new[] { Seq("Slash_Spin", "Elbow") },
                new[]
                {
                    Hits(1.6f, 1.03f, 0.3026f, 0.913f),
                    Hits(1.4f, 1.2293f, PerilousType.Grab, AttackHitboxSlot.Elbow, 1.1159f, 1.2293f)
                }),
            Move("Slash_StepTurn", BossMoveLayer.Active, 0f, 3f, 15f, 4f,
                new[] { Seq("Slash_StepTurn") },
                new[] { Hits(1.8f, 1.2367f, 0.99f, 1.2358f) }),
            Move("Kick", BossMoveLayer.Active, 0f, 3f, 30f, 5f,
                new[] { Seq("Attack_Slash", "Kick") },
                new[]
                {
                    Hits(1.2f, 1.0817f, 0.8818f, 1.0817f),
                    Hits(1.4f, 0.92f, 0.4322f, 0.5958f)
                }),
            Move("JumpThrust", BossMoveLayer.Active, 0f, 5f, 20f, 8f,
                new[] { Seq("JumpThrust") },
                new[] { Hits(2.2f, 1.55f, 0.8655f, 0.9632f) },
                PerilousType.JumpThrust),
            Move("Perilous_Sweep", BossMoveLayer.Active, 0f, 5f, 10f, 8f,
                new[] { Seq("Sweep") },
                new[] { Hits(2.4f, 1.75f, 1.145f, 1.2863f) },
                PerilousType.Sweep),
            Move("Bow_Air5", BossMoveLayer.Active, 0f, 3f, 30f, 10f,
                new[] { Seq("Dodge_Back", "Bow_Air5") }, new[] { NoHit(0.55f), NoHit(4.5f) }),

            Move("Bow_Heavy", BossMoveLayer.Interrupt, 0f, 99f, 1f, 8f,
                new[] { Seq("Bow_Heavy") }, new[] { NoHit(3f) }, dmg: 25, posture: 30f),

            Move("Kengeki_Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 40f, 0.5f,
                new[] { Seq("3050"), Seq("3055"), Seq("3065"), Seq("3071"), Seq("3076") },
                new[] { Hits(1.5f, 0.975f, 0.3615f, 0.5539f) }),
            Move("Kengeki_Double", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 3f,
                new[] { Seq("Kengeki_Double") },
                new[] { Hits(1.8f, 1.47f, 0.98f, 1.065f, 1.3163f, 1.47f) }),
            Move("Kengeki_Thrust", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
                new[] { Seq("Kengeki_Thrust") },
                new[] { Hits(2f, 1.45f, 1.1378f, 1.2893f) },
                PerilousType.Thrust),
            Move("Kengeki_Heavy", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
                new[] { Seq("Step_L", "Kengeki_Heavy"), Seq("Step_R", "Kengeki_Heavy") },
                new[] { NoHit(0.45f), Hits(1.8339f, 1.8339f, 1.425f, 1.8339f) }),
            Move("Kengeki_Bow", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 5f,
                new[] { Seq("3031", "3019", "3029"), Seq("3031", "3036") },
                new[] { NoHit(0.8f), NoHit(1.8f), NoHit(1.8f) }),
            Move("Kengeki_Air5", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 8f,
                new[] { Seq("Dodge_Back", "Bow_Air5") }, new[] { NoHit(0.55f), NoHit(4.5f) }),
            Move("Boat_Full", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 12f,
                new[] { Seq("Boat_Full") },
                new[] { Hit(3.2f, 0.2f, 1.76f) },
                extra: BossMoveExtra.HpBelow75),
            Move("Kengeki_Bow2Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("3018", "3015") },
                new[] { NoHit(1.6f), Hits(1.8f, 1.2535f, 1.0521f, 1.2535f) }),
            Move("Kengeki_JumpBow", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("3034", "3036", "3015") },
                new[] { NoHit(1.6f), NoHit(1.6f), Hits(1.8f, 1.2986f, 0.9896f, 1.2986f) }),
            Move("Bow_AirHeavy", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("Bow_AirHeavy") }, new[] { NoHit(2.2f) })
        };
    }
}
