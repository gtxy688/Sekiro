using System;
using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.FrameWork.Body
{

    // 按 CharacterConfig 管理角色 HP、架势、葫芦、回生和命数，通过事件通知表现层。
    // 崩解和死亡通过回调交给 CharacterBody 切状态。
    public class CombatStats
    {
        private readonly CharacterBody body;
        // 请求角色进入崩解状态；具体切换由 CharacterBody 决定。
        private readonly Action<PostureBreakSource> onPostureBreak;
        // 请求角色进入死亡状态：true 可等待回生，false 为真死。
        private readonly Action<bool> onDeath;

        public CombatStats(CharacterBody body, Action<PostureBreakSource> onPostureBreak, Action<bool> onDeath)
        {
            this.body = body;
            this.onPostureBreak = onPostureBreak;
            this.onDeath = onDeath;

            // 首次创建和复战重置共用同一套初始值，避免两条路径维护不同的赋值逻辑。
            ResetForEncounter();
        }

        // 恢复开场数值；只修改本角色的数据，不读取其他组件尚未复位的运行时状态。
        public void ResetForEncounter()
        {
            // 将配置值复制到运行时属性；战斗中改变 CurrentHP 等值不会修改配置资产。
            CharacterConfig config = body.Config;
            CurrentHP = config.MaxHP;
            GourdRemaining = config.GourdCount;
            ReviveRemaining = config.ReviveCount;
            LivesRemaining = config.LifeCount;
            CurrentPosture = 0f;
            IsPostureBroken = false;
            CurrentPostureBreakSource = PostureBreakSource.Attack;
            lastHitTime = 0f;
        }

        // ===== 每个角色各自保存的运行时数值 =====
        public int CurrentHP { get; private set; }
        public float CurrentPosture { get; private set; }
        public int GourdRemaining { get; private set; }
        public int ReviveRemaining { get; private set; }

        // 架势是否处于崩解状态（处决窗口内不自然回复）
        public bool IsPostureBroken { get; private set; }
        public PostureBreakSource CurrentPostureBreakSource { get; private set; }

        // 剩余命数（Boss 一阶段 2 条命；玩家 1 条）
        public int LivesRemaining { get; private set; }
        public bool IsDefeated => LivesRemaining <= 0;

        // 距上次受击的时间，用于架势自然回复的延迟判断
        private float lastHitTime;

        // 受击结算：扣血 + 涨架势。由 ReceiveHit（物理）或外部调用。
        public void TakeDamage(int healthDmg, float postureDmg, CharacterBody instigator = null)
        {
            if (CurrentHP <= 0) return; // 已死不再重复结算

            GameplaySettings.Load();
            bool skipHp = healthDmg > 0
                          && GameplaySettings.InfiniteHealth
                          && HitReactionUtil.IsPlayer(body);

            if (!skipHp)
                CurrentHP -= healthDmg;
            AccumulatePosture(postureDmg, true, PostureBreakSource.Attack, instigator);

            if (!skipHp)
            {
                // 事件总线：血条/音效/相机都靠这个驱动
                CombatEventBus.TriggerTakeDamage(body, healthDmg, Mathf.Max(CurrentHP, 0));
                CharacterConfig config = body.Config;
                CombatEventBus.TriggerHPChanged(body, Mathf.Max(CurrentHP, 0), config.MaxHP);

                if (CurrentHP <= 0)
                    HandleDeath();
            }
        }

        // 统一累计伤害、格挡或弹反带来的架势，并在达到上限时通知角色崩解。
        // allowBreak=false：本次累计不会导致崩解（只狼：完美弹反时自己的架势永不崩防）
        public bool AccumulatePosture(
            float amount,
            bool allowBreak = true,
            PostureBreakSource source = PostureBreakSource.Attack,
            CharacterBody instigator = null)
        {
            if (IsPostureBroken) return false; // 崩解中不累计

            CharacterConfig config = body.Config;
            float maxPosture = config.MaxPosture;

            if (allowBreak && amount > 0f
                && GameplaySettings.ShouldOneHitBreakBoss(body, source, instigator))
            {
                CurrentPosture = maxPosture;
                MarkCombatTime();
                CombatEventBus.TriggerPostureChanged(body, CurrentPosture, maxPosture);
                IsPostureBroken = true;
                CurrentPostureBreakSource = source;
                CombatEventBus.TriggerPostureBroken(body);
                CombatEventBus.TriggerFinisherOpportunityChanged(body, true);
                onPostureBreak(source);
                return true;
            }

            CurrentPosture = Mathf.Min(CurrentPosture + amount, maxPosture);
            MarkCombatTime();

            CombatEventBus.TriggerPostureChanged(body, CurrentPosture, maxPosture);

            if (allowBreak && CurrentPosture >= maxPosture)
            {
                IsPostureBroken = true;
                CurrentPostureBreakSource = source;
                // 崩解 → 崩解硬直（玩家击飞倒地 / Boss 处决窗口）
                CombatEventBus.TriggerPostureBroken(body);
                CombatEventBus.TriggerFinisherOpportunityChanged(body, true);
                onPostureBreak(source);
                return true;
            }

            return false;
        }

        // 刷新架势回复延迟。完美弹反自己不涨架势，但刀刃相撞仍算战斗，不能开始回条。
        public void MarkCombatTime()
        {
            lastHitTime = Time.time;
        }

        // 架势自然回复：停止受击超过 PostureDecayDelay 秒后，每秒回 PostureDecayRate
        public void UpdatePostureDecay()
        {
            // 崩解中 / 架势本来就是 0 → 不回复
            CharacterConfig config = body.Config;
            if (IsPostureBroken || CurrentPosture <= 0f) return;

            if (Time.time - lastHitTime >= config.PostureDecayDelay)
            {
                // 基础回复速度
                float rate = config.PostureDecayRate;

                // 格挡姿态使用配置中的回复倍率；是否开始回复仍由受击延迟控制。
                if (body.IsGuarding)
                {
                    rate *= config.GuardPostureRecoveryMultiplier;
                }

                // Boss 非线性回复：架势越高回越慢（反函数手感）
                if (config.PostureDecayInverse)
                {
                    rate *= 1f - CurrentPosture / config.MaxPosture;
                }

                CurrentPosture = Mathf.Max(0f, CurrentPosture - rate * Time.deltaTime);
                CombatEventBus.TriggerPostureChanged(body, CurrentPosture, config.MaxPosture);
            }
        }

        // 喝葫芦：有次数就能喝；满血也播动画、扣次数，HP 加完仍封顶
        public bool UseGourd()
        {
            CharacterConfig config = body.Config;
            if (GourdRemaining <= 0) return false;

            GourdRemaining--;
            CurrentHP = Mathf.Min(CurrentHP + config.HealAmount, config.MaxHP);

            CombatEventBus.TriggerGourdUsed(body, GourdRemaining);
            CombatEventBus.TriggerHPChanged(body, CurrentHP, config.MaxHP);
            return true;
        }

        // 死亡判定：有复活次数 → 进回生待机（先变暗，倒完再出选项）；否则直接真死
        private void HandleDeath()
        {
            body.AttackUninterruptible = false;
            body.DisableWeaponHit();
            if (ReviveRemaining > 0)
            {
                // 画面开始变暗（接 OnReviveAvailable）；倒完再出回生选项
                CombatEventBus.TriggerReviveAvailable(body);
                onDeath(true);
            }
            else
            {
                CombatEventBus.TriggerDeath(body);
                onDeath(false);
            }
        }

        // 回生时回满血、清空架势并扣除一次回生次数。
        public void Revive()
        {
            CharacterConfig config = body.Config;

            ReviveRemaining--;
            CurrentHP = config.MaxHP;
            CurrentPosture = 0f;
            IsPostureBroken = false;

            CombatEventBus.TriggerHPChanged(body, CurrentHP, config.MaxHP);
            CombatEventBus.TriggerPostureChanged(body, CurrentPosture, config.MaxPosture);
            CombatEventBus.TriggerRevived(body); // UI 隐藏回生提示
            if (HitReactionUtil.IsPlayer(body) && CombatManager.Instance != null && CombatManager.Instance.BossRef != null)
                CombatManager.Instance.BossRef.ResetConsecutiveTimesParried();
        }

        // 处决清一条命：扣命 → 没命了发胜利事件；还有命 → 重置架势回满血接着打
        public void ClearLife()
        {
            LivesRemaining--;
            CurrentPosture = 0f;
            IsPostureBroken = false;
            ResetConsecutiveTimesParried();
            CombatEventBus.TriggerFinisherOpportunityChanged(body, false);

            CombatEventBus.TriggerLifeCleared(body, LivesRemaining);

            if (LivesRemaining <= 0)
            {
                body.AttackUninterruptible = false;
                body.DisableWeaponHit();
                body.MoveDirection = Vector3.zero;
                CombatEventBus.TriggerVictory(body);
                return;
            }
            CharacterConfig config = body.Config;
            CurrentHP = config.MaxHP;
            CombatEventBus.TriggerPostureChanged(body, 0f, config.MaxPosture);
            CombatEventBus.TriggerHPChanged(body, CurrentHP, config.MaxHP);
        }

        // 崩解标志/架势条清掉，不切状态。倒地中再挨刀时先清再进受击，避免闪 Idle。
        public void ClearPostureBreak(float remainingRatio = 0f)
        {
            IsPostureBroken = false;
            CharacterConfig config = body.Config;
            float maxPosture = config.MaxPosture;
            CurrentPosture = Mathf.Clamp01(remainingRatio) * maxPosture;
            CombatEventBus.TriggerPostureChanged(body, CurrentPosture, maxPosture);
            CombatEventBus.TriggerFinisherOpportunityChanged(body, false);
        }

        private void ResetConsecutiveTimesParried()
        {
            body.ResetConsecutiveTimesParried();
        }
    }

}
