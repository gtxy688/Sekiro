    static BossAnimSequence Seq(params string[] states)
    {
        return new BossAnimSequence { states = states };
    }

    static BossAnimSequence SeqWin(string[] states, BossMoveWindow[] windows)
    {
        return new BossAnimSequence { states = states, windows = windows };
    }

    static HitPulse P(float start, float end, HitGrade grade = HitGrade.Light, bool ov = false,
        int dmg = 0, float posture = 0f)
    {
        return new HitPulse
        {
            start = start, end = end, hitGrade = grade, overrideCombat = ov,
            baseDamage = dmg, postureDamage = posture
        };
    }

    static ArrowSpawnCue A(float time, HitGrade grade = HitGrade.Light, bool ov = false,
        int dmg = 0, float posture = 0f)
    {
        return new ArrowSpawnCue
        {
            time = time, hitGrade = grade, overrideCombat = ov,
            baseDamage = dmg, postureDamage = posture
        };
    }

    static BossMoveWindow Win(
        float hitStart, float recover, float comboEnd, float duration,
        HitPulse[] pulses = null, ArrowSpawnCue[] arrows = null,
        PerilousType perilous = PerilousType.None,
        AttackHitboxSlot slot = AttackHitboxSlot.Weapon,
        float rotateEnd = 0.35f, float transition = 0.1f,
        HitGrade grade = HitGrade.Light, bool ov = false,
        int dmg = 0, float posture = 0f, float knockback = 0f,
        bool waitAnimEnd = false)
    {
        var w = new BossMoveWindow
        {
            hitStartTime = hitStart,
            recoverStart = recover,
            comboWindowEnd = comboEnd,
            stateDuration = duration,
            rotateEnd = rotateEnd,
            transitionDuration = transition,
            perilous = perilous,
            hitboxSlot = slot,
            hitPulses = pulses ?? new HitPulse[0],
            arrowCues = arrows ?? new ArrowSpawnCue[0],
            sfxCues = new AttackSfxCue[0],
            hitGrade = grade,
            overrideCombat = ov,
            baseDamage = dmg,
            postureDamage = posture,
            knockback = knockback,
            waitAnimEnd = waitAnimEnd
        };
        AttackWindowSync.CoverDuration(w);
        return w;
    }

    static BossMoveEntry Move(
        string id, BossMoveLayer layer,
        float min, float max, float weight, float cooldown,
        BossAnimSequence[] sequences, BossMoveWindow[] windows,
        PerilousType perilous = PerilousType.None,
        BossMoveExtra extra = BossMoveExtra.None,
        int dmg = 10, float posture = 10f,
        HitGrade grade = HitGrade.Light, float knockback = 0f)
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
            postureDamage = posture,
            hitGrade = grade,
            knockback = knockback
        };
    }
