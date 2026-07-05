using UnityEngine;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 攻击状态 — 三段连招帧计数流程。
    /// 分前摇(Startup)→判定(Active)→后摇(Recovery)三段。
    /// Recovery 阶段按攻击键可进入下一段连招。
    /// </summary>
    public class AttackState : PlayerBaseState
    {
        public override int Priority => 1;

        private enum AttackPhase { Startup, Active, Recovery }

        private AttackPhase _phase;
        private float _phaseTimer;
        private int _comboStep;
        private bool _hasHit;

        /// <summary>单帧时长（60fps）</summary>
        private const float FrameDuration = 1f / 60f;

        /// <summary>攻击帧数据：前摇/判定/后摇</summary>
        private static readonly (float startup, float active, float recovery)[] ComboData =
        {
            (8f, 4f, 10f),   // 第一段
            (6f, 4f, 10f),   // 第二段
            (10f, 6f, 14f)   // 第三段
        };

        /// <summary>
        /// 进入攻击状态，开始第一段或下一段连招。
        /// </summary>
        public override void Enter()
        {
            _hasHit = false;

            // 获取连招段数
            _comboStep = Ctx.CombatController != null
                ? Mathf.Clamp(Ctx.CombatController.ComboCount - 1, 0, 2)
                : 0;

            var data = ComboData[_comboStep];

            // 播放对应段攻击动画
            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = true;
                string animName = _comboStep switch
                {
                    0 => "attack1",
                    1 => "attack2",
                    _ => "attack3"
                };
                Ctx.Animator.SetTrigger(animName);
            }

            // 开始前摇
            _phase = AttackPhase.Startup;
            _phaseTimer = data.startup * FrameDuration;
        }

        /// <summary>
        /// 每帧执行：推进攻击阶段。
        /// </summary>
        public override void Execute()
        {
            _phaseTimer -= Time.deltaTime;

            switch (_phase)
            {
                case AttackPhase.Startup:
                    if (_phaseTimer <= 0f)
                    {
                        _phase = AttackPhase.Active;
                        _phaseTimer = ComboData[_comboStep].active * FrameDuration;
                    }
                    break;

                case AttackPhase.Active:
                    if (!_hasHit)
                    {
                        _hasHit = TryHitEnemy();
                    }

                    if (_phaseTimer <= 0f)
                    {
                        _phase = AttackPhase.Recovery;
                        _phaseTimer = ComboData[_comboStep].recovery * FrameDuration;
                    }
                    break;

                case AttackPhase.Recovery:
                    if (_phaseTimer <= 0f)
                    {
                        // 攻击结束，回到地面状态
                        PlayerSM.TransitionTo<GroundedState>();
                    }
                    else if (Ctx.InputReader != null && Ctx.InputReader.IsAttackPressed())
                    {
                        // 连招：下一段攻击（如果还有）
                        if (_comboStep < 2)
                        {
                            Ctx.CombatController?.HandleAttack();
                            PlayerSM.TransitionTo<AttackState>();
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 在判定帧检测是否命中敌人。
        /// 使用 Physics.OverlapSphere 检测前方碰撞体。
        /// </summary>
        private bool TryHitEnemy()
        {
            if (Ctx.Transform == null) return false;

            float hitRadius = 1.5f;
            Vector3 hitPos = Ctx.Transform.position + Ctx.Transform.forward * 1.5f;

            Collider[] hits = Physics.OverlapSphere(hitPos, hitRadius);

            for (int i = 0; i < hits.Length; i++)
            {
                // 检测 Boss 层（Layer 设定在预制体时配置）
                if (hits[i].gameObject.layer == LayerMask.NameToLayer("Boss"))
                {
                    // 命中 Boss → 通过事件传递伤害
                    float damage = Ctx.Stats != null ? Ctx.Stats.attack : 100f;
                    Sekiro.Core.Events.CombatEvents.RaiseBossDamaged(damage);
                    Ctx.HitStopManager?.Trigger(0.033f);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 退出攻击状态。
        /// </summary>
        public override void Exit()
        {
            if (Ctx.Animator != null)
                Ctx.Animator.applyRootMotion = false;
        }
    }
}
