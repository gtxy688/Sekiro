using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.Combat
{

    // 打铁火花用两刀最近点，不用 Hurtbox 胶囊表面。
    // 玩法判定仍走 SphereCast/Overlap；火花是表现，要对刀刃而不是胸口。
    public static class CombatFxPoint
    {
        static Hitbox FxHitbox(CharacterBody body)
        {
            if (body == null) return null;
            return body.ActiveHitbox != null ? body.ActiveHitbox : body.Weapon;
        }

        // 近战：两刀中点；箭：贴防守方刀刃（箭没有攻击方 Hitbox，BetweenWeapons 会落到两人刀之间的半空）。
        public static Vector3 ForDeflect(
            CharacterBody attacker, CharacterBody defender, Vector3 hitPoint, bool isProjectile)
        {
            if (isProjectile)
                return OnDefenderWeapon(defender, hitPoint);
            return BetweenWeapons(attacker, defender, hitPoint);
        }

        public static Vector3 BetweenWeapons(CharacterBody a, CharacterBody b, Vector3 fallback)
        {
            Hitbox ha = FxHitbox(a);
            Hitbox hb = FxHitbox(b);
            if (ha != null && hb != null)
                return ClosestMid(Hilt(ha), Tip(ha), Hilt(hb), Tip(hb));

            if (ha != null)
            {
                Vector3 onHurt = ClosestOnHurtbox(b, Tip(ha));
                if (onHurt != fallback)
                    return Vector3.Lerp(Tip(ha), onHurt, 0.5f);
            }

            return fallback;
        }

        public static Vector3 BetweenHitboxes(Hitbox a, Hitbox b, Vector3 fallback)
        {
            if (a == null || b == null) return fallback;
            return ClosestMid(Hilt(a), Tip(a), Hilt(b), Tip(b));
        }

        // 箭命中点在身上，火花改贴防守方刀身离命中点最近处。
        static Vector3 OnDefenderWeapon(CharacterBody defender, Vector3 hitPoint)
        {
            Hitbox hb = FxHitbox(defender);
            if (hb == null) return hitPoint;

            Vector3 tip = Tip(hb);
            Vector3 hilt = Hilt(hb);
            Vector3 onBlade = ClosestOnSegment(hilt, tip, hitPoint);
            // 略向命中点收一点，避免纯贴刀心时看起来飘在刃里。
            return Vector3.Lerp(onBlade, hitPoint, 0.15f);
        }

        static Vector3 Tip(Hitbox h)
        {
            return h.transform.position;
        }

        static Vector3 Hilt(Hitbox h)
        {
            return h.transform.parent != null ? h.transform.parent.position : h.transform.position;
        }

        static Vector3 ClosestOnHurtbox(CharacterBody body, Vector3 from)
        {
            if (body == null) return from;
            Hurtbox hurt = body.GetComponentInChildren<Hurtbox>();
            Collider col = hurt != null ? hurt.GetComponent<Collider>() : null;
            if (col == null) col = body.GetComponent<Collider>();
            if (col == null) return from;
            return col.ClosestPoint(from);
        }

        static Vector3 ClosestOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-8f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }

        // 两段线段最近点的中点。
        static Vector3 ClosestMid(Vector3 p1, Vector3 q1, Vector3 p2, Vector3 q2)
        {
            Vector3 d1 = q1 - p1;
            Vector3 d2 = q2 - p2;
            Vector3 r = p1 - p2;
            float a = Vector3.Dot(d1, d1);
            float e = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            const float eps = 1e-8f;

            float s;
            float t;
            if (a <= eps && e <= eps)
                return (p1 + p2) * 0.5f;

            if (a <= eps)
            {
                s = 0f;
                t = Mathf.Clamp01(f / e);
            }
            else
            {
                float c = Vector3.Dot(d1, r);
                if (e <= eps)
                {
                    t = 0f;
                    s = Mathf.Clamp01(-c / a);
                }
                else
                {
                    float b = Vector3.Dot(d1, d2);
                    float denom = a * e - b * b;
                    s = Mathf.Abs(denom) > eps ? Mathf.Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Mathf.Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Mathf.Clamp01((b - c) / a);
                    }
                }
            }

            Vector3 c1 = p1 + d1 * s;
            Vector3 c2 = p2 + d2 * t;
            return (c1 + c2) * 0.5f;
        }
    }

}
