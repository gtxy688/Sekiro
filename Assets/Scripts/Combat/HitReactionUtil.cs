// 玩家挨 Boss 时的受击/格挡/弹反选动画。Boss 被打不走这里。
public static class HitReactionUtil
{
    public static bool IsPlayer(CharacterBody body)
    {
        return CombatManager.Instance != null && body != null && body == CombatManager.Instance.PlayerRef;
    }

    public static string PerfectParryAnim(HitData hit)
    {
        if (hit.isProjectile && hit.hitGrade == HitGrade.Heavy)
            return "Deflect_HeavyArrow";
        if (hit.hitGrade == HitGrade.Light)
            return "Deflect_Slash";
        return "Deflect_HeavySlash";
    }

    public static bool IsMeleeHeavyPierce(HitData hit)
    {
        return !hit.isProjectile && hit.hitGrade == HitGrade.Heavy;
    }

    public static bool IsArrowHeavyGuard(HitData hit)
    {
        return hit.isProjectile && hit.hitGrade == HitGrade.Heavy;
    }

    // 连续举刀放刀仍算格挡；但 Boat / Air5 等多段招可在抬刀期弹反。
    public static bool IsMultiHitParryException(HitData hit)
    {
        if (hit.attacker == null) return false;
        BossMoveEntry entry = hit.attacker.CurrentMoveEntry;
        if (entry != null)
        {
            string id = entry.id;
            if (id == "Boat" || id == "Boat_Full" || id == "Bow_Air5" || id == "Kengeki_Air5")
                return true;
        }

        AttackConfig atk = hit.attacker.ActiveAttack;
        return atk != null && atk.hitPulses != null && atk.hitPulses.Length > 1;
    }

    public static string GuardHurtAnim(CharacterBody body)
    {
        return body.ResolveHurtAnim(HurtContext.Guard);
    }

    public static string BrokenAnim(CharacterBody body)
    {
        if (body != null && body.Config != null && !string.IsNullOrEmpty(body.Config.HurtAnim_Broken))
            return body.Config.HurtAnim_Broken;
        return "Stagger_Broken";
    }

    public static string StandingAnim(CharacterBody body)
    {
        if (body != null && body.Config != null && !string.IsNullOrEmpty(body.Config.HurtAnim_Standing))
            return body.Config.HurtAnim_Standing;
        return "Standing";
    }

    public static string MidToGuardAnim(CharacterBody body)
    {
        if (body != null && body.Config != null && !string.IsNullOrEmpty(body.Config.HurtAnim_MidToGuard))
            return body.Config.HurtAnim_MidToGuard;
        return "MidToGuard";
    }

    // Light 任意等级都刷新；Mid 只有升 Heavy 才刷新（播 Repeat）；Heavy 同级播 Repeat。
    public static bool ShouldRefreshHurt(HitGrade current, HitGrade incoming, out bool heavyRepeat)
    {
        heavyRepeat = false;
        if (current == HitGrade.Light)
            return true;
        if (current == HitGrade.Mid)
        {
            if (incoming == HitGrade.Heavy)
            {
                heavyRepeat = true;
                return true;
            }
            return false;
        }

        if (incoming == HitGrade.Heavy)
        {
            heavyRepeat = true;
            return true;
        }
        return false;
    }

    public static string UnguardedAnim(CharacterBody body, HitGrade grade, bool lightRepeat, bool heavyRepeat)
    {
        CharacterConfig cfg = body != null ? body.Config : null;
        if (heavyRepeat)
            return Pick(body, cfg != null ? cfg.HurtAnim_HeavyRepeat : null, "Hurt_HeavyRepeat", "Hurt_Heavy");
        if (grade == HitGrade.Mid)
            return Pick(body, cfg != null ? cfg.HurtAnim_Mid : null, "Hurt_Mid", "Hurt_Heavy");
        if (grade == HitGrade.Heavy)
            return Pick(body, cfg != null ? cfg.HurtAnim_Heavy : null, "Hurt_Heavy", "Hurt_Ground");
        if (lightRepeat)
            return Pick(body, cfg != null ? cfg.HurtAnim_Light2 : null, "Hurt_Light2", "Hurt_Light", "Hurt_Ground");
        return Pick(body, cfg != null ? cfg.HurtAnim_Normal : null, "Hurt_Light", "Hurt_Ground");
    }

    static string Pick(CharacterBody body, params string[] names)
    {
        UnityEngine.Animator anim = body != null ? body.Animator : null;
        for (int i = 0; i < names.Length; i++)
        {
            if (string.IsNullOrEmpty(names[i])) continue;
            if (anim == null || AnimUtil.HasState(anim, names[i]))
                return names[i];
        }
        for (int i = 0; i < names.Length; i++)
        {
            if (!string.IsNullOrEmpty(names[i])) return names[i];
        }
        return "Hurt_Light";
    }
}
