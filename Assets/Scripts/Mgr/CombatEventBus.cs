using UnityEngine;
using System;

using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
namespace ARPG.Mgr
{

    // 表现层用于区分格挡与完美弹反的音效和火花。
    public enum DeflectType
    {
        Normal,
        Perfect
    }

    // 战斗逻辑发布事件，UI、音效和相机按需订阅；订阅方停用时负责解绑。
    public static class CombatEventBus
    {
        // 武器被格挡（位置, 防御类型），用于音效和火花。
        public static event Action<Vector3, DeflectType> OnWeaponDeflected;

        // 角色受伤（受击者, 伤害值, 剩余血量）
        public static event Action<CharacterBody, int, int> OnTakeDamage;

        // 数值事件携带当前值，订阅者据此更新血量与架势显示。
        // 血量变化（角色, 当前血量, 最大血量）
        public static event Action<CharacterBody, int, int> OnHPChanged;
        // 架势变化（角色, 当前架势, 最大架势）
        public static event Action<CharacterBody, float, float> OnPostureChanged;
        // 架势崩解（角色），用于崩解提示。
        public static event Action<CharacterBody> OnPostureBroken;
        // 忍杀机会变化（目标, 是否可忍杀），控制 UI 红点。
        public static event Action<CharacterBody, bool> OnFinisherOpportunityChanged;
        // 葫芦使用（角色, 剩余次数）
        public static event Action<CharacterBody, int> OnGourdUsed;
        // 角色死亡
        public static event Action<CharacterBody> OnDeath;
        // 复活可用（角色）—— 倒地开始，画面变暗
        public static event Action<CharacterBody> OnReviveAvailable;
        // 倒地结束（角色）—— 弹出回生选项，开始接收输入
        public static event Action<CharacterBody> OnReviveChoiceReady;
        // 最后一条 Boss 命数处决完毕，触发胜利结算。
        public static event Action<CharacterBody> OnVictory;
        // 清命（角色, 剩余命数），更新忍杀灯。
        public static event Action<CharacterBody, int> OnLifeCleared;
        // 复活成功（角色），隐藏回生提示。
        public static event Action<CharacterBody> OnRevived;
        // 锁定状态变化（是否锁定），同步准星与相机模式。
        public static event Action<bool> OnLockOnChanged;

        // 战斗表现事件。
        // 危险攻击类型，用于显示“危”字。
        public static event Action<PerilousType> OnPerilousAttack;
        // 忍杀触发（位置）—— 处决音效/特效
        public static event Action<Vector3> OnFinisherTriggered;
        // 处决完整生命周期（位置, 玩家, 受害者, 忍杀类型）—— 驱动处决运镜与特写
        public static event Action<Vector3, CharacterBody, CharacterBody, FinisherKind> OnFinisherStarted;
        public static event Action<CharacterBody, CharacterBody> OnFinisherEnded;
        // 相机震动（强度）—— 弹反/崩解/处决时触发
        public static event Action<float> OnCameraShake;
        // 重箭被格挡/弹反（角色, 是否完美弹反）—— 玩家会借力后滑，镜头跟随下压后拉
        public static event Action<CharacterBody, bool> OnHeavyArrowDefended;
        // Boss JumpThrust 整段：镜头上抬 + 随 Boss 高度跟随（active, boss, 起手地面 Y）
        public static event Action<bool, CharacterBody, float> OnJumpThrustCamera;
        // 出招音效（音频片段, 世界坐标），音频由攻击配置提供。
        public static event Action<AudioClip, Vector3> OnAttackSfx;
        // 出刀瞬间（Hitbox 打开）—— 玩家挥刀刀光
        public static event Action<CharacterBody> OnAttackSwingStart;
        public static event Action<CharacterBody> OnAttackSwingEnd;
        // Boss 弓段出箭
        public static event Action<CharacterBody> OnArrowReleased;

        // 战斗模块通过这些入口发布事件，监听列表由本类维护。

        public static void TriggerWeaponDeflected(Vector3 hitPoint, DeflectType type)
        {
            OnWeaponDeflected?.Invoke(hitPoint, type);
        }

        public static void TriggerTakeDamage(CharacterBody victim, int dmg, int currentHp)
        {
            OnTakeDamage?.Invoke(victim, dmg, currentHp);
        }

        public static void TriggerHPChanged(CharacterBody c, int currentHp, int maxHp)
        {
            OnHPChanged?.Invoke(c, currentHp, maxHp);
        }

        public static void TriggerPostureChanged(CharacterBody c, float posture, float maxPosture)
        {
            OnPostureChanged?.Invoke(c, posture, maxPosture);
        }

        public static void TriggerPostureBroken(CharacterBody c)
        {
            OnPostureBroken?.Invoke(c);
        }

        public static void TriggerFinisherOpportunityChanged(CharacterBody target, bool available)
        {
            OnFinisherOpportunityChanged?.Invoke(target, available);
        }

        public static void TriggerGourdUsed(CharacterBody c, int remaining)
        {
            OnGourdUsed?.Invoke(c, remaining);
        }

        public static void TriggerDeath(CharacterBody c)
        {
            OnDeath?.Invoke(c);
        }

        public static void TriggerReviveAvailable(CharacterBody c)
        {
            OnReviveAvailable?.Invoke(c);
        }

        public static void TriggerReviveChoiceReady(CharacterBody c)
        {
            OnReviveChoiceReady?.Invoke(c);
        }

        public static void TriggerVictory(CharacterBody c)
        {
            OnVictory?.Invoke(c);
        }

        public static void TriggerLifeCleared(CharacterBody c, int remainingLives)
        {
            OnLifeCleared?.Invoke(c, remainingLives);
        }

        public static void TriggerRevived(CharacterBody c)
        {
            OnRevived?.Invoke(c);
        }

        public static void TriggerLockOnChanged(bool isLocked)
        {
            OnLockOnChanged?.Invoke(isLocked);
        }

        public static void TriggerPerilousAttack(PerilousType type)
        {
            OnPerilousAttack?.Invoke(type);
        }

        public static void TriggerFinisherStarted(
            Vector3 pos,
            CharacterBody player,
            CharacterBody victim,
            FinisherKind kind = FinisherKind.Ground)
        {
            OnFinisherTriggered?.Invoke(pos);
            OnFinisherStarted?.Invoke(pos, player, victim, kind);
        }

        public static void TriggerFinisherEnded(CharacterBody player, CharacterBody victim)
        {
            OnFinisherEnded?.Invoke(player, victim);
        }

        public static void TriggerCameraShake(float intensity)
        {
            OnCameraShake?.Invoke(intensity);
        }

        public static void TriggerHeavyArrowDefended(CharacterBody body, bool perfect)
        {
            OnHeavyArrowDefended?.Invoke(body, perfect);
        }

        public static void TriggerJumpThrustCamera(bool active, CharacterBody boss = null, float bossBaseY = 0f)
        {
            OnJumpThrustCamera?.Invoke(active, boss, bossBaseY);
        }

        public static void TriggerAttackSfx(AudioClip clip, Vector3 worldPos)
        {
            if (clip == null) return;
            OnAttackSfx?.Invoke(clip, worldPos);
        }

        public static void TriggerAttackSwingStart(CharacterBody attacker)
        {
            if (attacker == null) return;
            OnAttackSwingStart?.Invoke(attacker);
        }

        public static void TriggerAttackSwingEnd(CharacterBody attacker)
        {
            if (attacker == null) return;
            OnAttackSwingEnd?.Invoke(attacker);
        }

        public static void TriggerArrowReleased(CharacterBody shooter)
        {
            if (shooter == null) return;
            OnArrowReleased?.Invoke(shooter);
        }
    }
}
