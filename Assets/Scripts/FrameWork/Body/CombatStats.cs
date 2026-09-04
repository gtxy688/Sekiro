using System;
using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.FrameWork.Body
{

    // 战斗数值模块（CharacterBody 重构：HP/架势/葫芦/命数的生命周期与结算）。
    // 职责：
    //   1. 数值状态：CurrentHP/CurrentPosture/GourdRemaining/ReviveRemaining/LivesRemaining/崩解标志
    //   2. 结算逻辑：TakeDamage / AccumulatePosture / UpdatePostureDecay / UseGourd / Revive / ClearLife
    //   3. 死亡与崩解的"事件上报"：触发事件总线后，通过构造注入的委托请求 Façade 切状态
    // 约束：
    // - 数值只从 CharacterConfig（SO）读取；事件全部走 CombatEventBus，模块不持有表现层引用；
    // - 崩解 → ForcePostureBroken、死亡 → DeadState 的切换编排权在 Façade（onPostureBreak/onDeath 委托），
    //   模块自己不碰状态机（"模块请求、Façade 编排"）；
    // - 架构红线：不判状态类型，只依赖 body 的语义化标志（IsGuarding 等）。
    public class CombatStats
    {
        private readonly CharacterBody body;
        // 崩解硬直入口（玩家击飞倒地 / Boss 处决窗口）——由 Façade 的 ForcePostureBroken 编排
        private readonly Action<PostureBreakSource> onPostureBreak;
        // 死亡入口（true=进回生待机，false=真死）——由 Façade 构造 DeadState 并切入
        private readonly Action<bool> onDeath;

        public CombatStats(CharacterBody body, Action<PostureBreakSource> onPostureBreak, Action<bool> onDeath)
        {
            this.body = body;
            this.onPostureBreak = onPostureBreak;
            this.onDeath = onDeath;

            ResetForEncounter();
        }

        // 复战重置：把数值恢复到「战斗刚开始」的瞬间。
        //
        // 这段原本只写在构造函数里——意味着唯一能调用它的方式是销毁重建，也就是重载场景。
        // 复战 / 连战要走「原地重开」，不重载场景，所以必须把它抽成可重复调用的方法。
        // 构造函数现在也走它，保证两条路径的初始状态完全一致，不会哪天改了一处忘了另一处。
        public void ResetForEncounter()
        {
            // 原 InitCombat：从 Config 读取初始数值（Awake 时机由 Façade 保证）
            CharacterConfig config = body.Config;
            if (config != null)
            {
                CurrentHP = config.MaxHP;
                GourdRemaining = config.GourdCount;
                ReviveRemaining = config.ReviveCount;
                LivesRemaining = config.LifeCount;
            }
            CurrentPosture = 0f;
            IsPostureBroken = false;
            CurrentPostureBreakSource = PostureBreakSource.Attack;
            lastHitTime = 0f;
        }

        // ===== 运行时数值状态（原 CharacterBody 属性迁入）=====
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
                if (config != null)
                    CombatEventBus.TriggerHPChanged(body, Mathf.Max(CurrentHP, 0), config.MaxHP);

                if (CurrentHP <= 0)
                    HandleDeath();
            }
        }

        // 累计架势。防御/弹反也会加少量（M9 细则接），这里统一入口。
        // allowBreak=false：本次累计不会导致崩解（只狼：完美弹反时自己的架势永不崩防）
        public bool AccumulatePosture(
            float amount,
            bool allowBreak = true,
            PostureBreakSource source = PostureBreakSource.Attack,
            CharacterBody instigator = null)
        {
            if (IsPostureBroken) return false; // 崩解中不累计

            CharacterConfig config = body.Config;
            float maxPosture = config != null ? config.MaxPosture : 100f;

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
            // 崩解中 / 没配置 / 架势本来就是 0 → 不回复
            CharacterConfig config = body.Config;
            if (IsPostureBroken || config == null || CurrentPosture <= 0f) return;

            if (Time.time - lastHitTime >= config.PostureDecayDelay)
            {
                // 基础回复速度
                float rate = config.PostureDecayRate;

                // 按住格挡 2s 后回复 ×5（只狼：格挡姿态回架势快）
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

        // 喝葫芦（M16）：有次数就能喝；满血也播动画、扣次数，HP 加完仍封顶
        public bool UseGourd()
        {
            CharacterConfig config = body.Config;
            if (config == null) return false;
            if (GourdRemaining <= 0) return false;

            GourdRemaining--;
            CurrentHP = Mathf.Min(CurrentHP + config.HealAmount, config.MaxHP);

            CombatEventBus.TriggerGourdUsed(body, GourdRemaining);
            CombatEventBus.TriggerHPChanged(body, CurrentHP, config.MaxHP);
            return true;
        }

        // 死亡判定（M14）：有复活次数 → 进回生待机（先变暗，倒完再出选项）；否则直接真死
        private void HandleDeath()
        {
            body.IsAttacking = false;
            body.AttackUninterruptible = false;
            body.DisableWeaponHit();
            if (ReviveRemaining > 0)
            {
                // 画面开始变暗（M13 接 OnReviveAvailable）；倒完再出回生选项
                CombatEventBus.TriggerReviveAvailable(body);
                onDeath(true);
            }
            else
            {
                CombatEventBus.TriggerDeath(body);
                onDeath(false);
            }
        }

        // 复活（M14）：回满血 + 架势清零（用户决策：直接回满），扣除一次复活次数
        public void Revive()
        {
            CharacterConfig config = body.Config;
            if (config == null) return;

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

        // 处决清一条命（M10 用）：扣命 → 没命了发胜利事件；还有命 → 重置架势回满血接着打
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
                body.IsAttacking = false;
                body.AttackUninterruptible = false;
                body.DisableWeaponHit();
                body.MoveDirection = Vector3.zero;
                CombatEventBus.TriggerVictory(body);
                return;
            }
            CharacterConfig config = body.Config;
            CurrentHP = config != null ? config.MaxHP : CurrentHP;
            CombatEventBus.TriggerPostureChanged(body, 0f, config != null ? config.MaxPosture : 100f);
            CombatEventBus.TriggerHPChanged(body, CurrentHP, config != null ? config.MaxHP : 0);
        }

        // 崩解标志/架势条清掉，不切状态。倒地中再挨刀时先清再进受击，避免闪 Idle。
        public void ClearPostureBreak(float remainingRatio = 0f)
        {
            IsPostureBroken = false;
            CharacterConfig config = body.Config;
            float maxPosture = config != null ? config.MaxPosture : 100f;
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
