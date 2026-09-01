using System.Collections.Generic;
using System.Text;

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

using ARPG.Boss;
using ARPG.Configs;
namespace ARPG.Editor
{
    // 战斗配置的静态校验规则。校验窗口和构建前校验共用这一份，避免两套规则各自漂移。
    //
    // 为什么需要它：招式表里的动画状态名写错，运行状态机不会报错，只是那个招式永远不触发——
    // 排查一次要翻遍 Animator 和招式表。多一个 Boss 就是几十条新招式，出错概率和排查成本都翻倍。
    public enum ValidationSeverity
    {
        Error,      // 一定出错，构建必须拦下
        Warning,    // 可疑，但可能是有意配置
    }

    public class ValidationIssue
    {
        public ValidationSeverity Severity;
        public string Message;
        public string Where;        // 人类可读的定位，如 "GenichiroMoveTable / 招式[Air5] / 分支2 / 段1"
        public Object Target;       // 点击跳转的目标资产

        public override string ToString()
        {
            return $"[{Severity}] {Where}：{Message}";
        }
    }

    public static class ArpgValidationRules
    {
        const string BossControllerPath = "Assets/Anim/boss.controller";
        const string PlayerControllerPath = "Assets/Anim/player.controller";

        // 判定窗口短于这个秒数就当"假红条"。
        // 原因：进招第 0 帧 animTime=0，窗口 [0, 0.01) 会让 `0 >= 0 && 0 < 0.01` 成立，
        // 本该无判定的段（弓段、垫步）会亮一帧刀，表现是"莫名其妙被打"。
        // 真正要关判定就把四个字段写成相等，别缩成一小段。
        const float FakeWindowSeconds = 0.02f;

        public static List<ValidationIssue> RunAll()
        {
            List<ValidationIssue> issues = new List<ValidationIssue>();
            HashSet<string> bossStates = CollectStateNames(BossControllerPath);
            HashSet<string> playerStates = CollectStateNames(PlayerControllerPath);

            if (bossStates == null)
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Where = "Animator",
                    Message = $"读不到 {BossControllerPath}，跳过 Boss 动画状态名校验。",
                });
            if (playerStates == null)
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Where = "Animator",
                    Message = $"读不到 {PlayerControllerPath}，跳过玩家动画状态名校验。",
                });

            ValidateMoveTables(issues, bossStates);
            ValidateAttackConfigs(issues, playerStates);
            return issues;
        }

        // ---------- 招式表 ----------

        static void ValidateMoveTables(List<ValidationIssue> issues, HashSet<string> bossStates)
        {
            string[] guids = AssetDatabase.FindAssets("t:BossMoveTable");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                BossMoveTable table = AssetDatabase.LoadAssetAtPath<BossMoveTable>(path);
                if (table == null) continue;
                ValidateMoveTable(table, path, issues, bossStates);
            }
        }

        static void ValidateMoveTable(
            BossMoveTable table, string assetPath, List<ValidationIssue> issues, HashSet<string> bossStates)
        {
            if (table.moves == null || table.moves.Length == 0)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Where = assetPath,
                    Message = "招式表是空的。",
                    Target = table,
                });
                return;
            }

            HashSet<string> seenIds = new HashSet<string>();

            for (int i = 0; i < table.moves.Length; i++)
            {
                BossMoveEntry entry = table.moves[i];
                if (entry == null) continue;

                string where = $"{table.name} / 招式#{i}";

                if (string.IsNullOrEmpty(entry.id))
                {
                    issues.Add(Issue(ValidationSeverity.Error, where, "id 为空，选招日志没法定位。", table));
                }
                else
                {
                    if (!seenIds.Add(entry.id))
                        issues.Add(Issue(ValidationSeverity.Error, where, $"id「{entry.id}」重复。", table));
                    where = $"{table.name} / 招式[{entry.id}]";
                }

                if (entry.minRange > entry.maxRange)
                    issues.Add(Issue(ValidationSeverity.Error, where,
                        $"minRange({entry.minRange}) > maxRange({entry.maxRange})，这个招永远选不中。", table));

                if (entry.weight <= 0f)
                    issues.Add(Issue(ValidationSeverity.Warning, where, "weight <= 0，不会被抽到。", table));

                if (entry.sequences == null || entry.sequences.Length == 0)
                {
                    issues.Add(Issue(ValidationSeverity.Error, where, "没有动画分支（sequences 为空）。", table));
                }
                else
                {
                    for (int s = 0; s < entry.sequences.Length; s++)
                    {
                        BossAnimSequence seq = entry.sequences[s];
                        if (seq == null) continue;
                        string seqWhere = entry.sequences.Length > 1
                            ? $"{where} / 分支{s + 1}"
                            : where;

                        if (seq.states == null || seq.states.Length == 0)
                        {
                            issues.Add(Issue(ValidationSeverity.Error, seqWhere, "分支没有动画状态名。", table));
                            continue;
                        }

                        for (int k = 0; k < seq.states.Length; k++)
                        {
                            string state = seq.states[k];
                            if (string.IsNullOrEmpty(state))
                            {
                                issues.Add(Issue(ValidationSeverity.Error, seqWhere, $"第 {k + 1} 段动画名为空。", table));
                                continue;
                            }
                            if (bossStates != null && !bossStates.Contains(state))
                            {
                                issues.Add(Issue(ValidationSeverity.Error, seqWhere,
                                    $"Animator 里找不到状态「{state}」，这个招不会触发。", table));
                            }
                        }
                    }
                }

                ValidateWindows(entry.windows, where + " / 招级窗口", table, issues);
                if (entry.sequences != null)
                {
                    for (int s = 0; s < entry.sequences.Length; s++)
                    {
                        if (entry.sequences[s] == null) continue;
                        string seqWhere = entry.sequences.Length > 1
                            ? $"{where} / 分支{s + 1} / 段窗口"
                            : $"{where} / 段窗口";
                        ValidateWindows(entry.sequences[s].windows, seqWhere, table, issues);
                    }
                }
            }
        }

        static void ValidateWindows(
            BossMoveWindow[] windows, string where, BossMoveTable table, List<ValidationIssue> issues)
        {
            if (windows == null) return;

            for (int i = 0; i < windows.Length; i++)
            {
                BossMoveWindow w = windows[i];
                if (w == null) continue;
                string wWhere = windows.Length > 1 ? $"{where}[{i + 1}]" : where;

                ValidateWindowTiming(
                    w.hitStartTime, w.recoverStart, w.comboWindowEnd, w.stateDuration, wWhere, table, issues);
                ValidatePulses(w.hitPulses, w.stateDuration, wWhere, table, issues);
                ValidateCueTimes(w.sfxCues, w.stateDuration, wWhere, "音效", table, issues);
                ValidateCueTimes(w.arrowCues, w.stateDuration, wWhere, "出箭", table, issues);
            }
        }

        // ---------- 玩家 AttackConfig ----------

        static void ValidateAttackConfigs(List<ValidationIssue> issues, HashSet<string> playerStates)
        {
            string[] guids = AssetDatabase.FindAssets("t:AttackConfig");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                AttackConfig cfg = AssetDatabase.LoadAssetAtPath<AttackConfig>(path);
                if (cfg == null) continue;

                string where = cfg.name;

                if (string.IsNullOrEmpty(cfg.AnimName))
                {
                    issues.Add(Issue(ValidationSeverity.Error, where, "AnimName 为空。", cfg));
                }
                else if (playerStates != null && !playerStates.Contains(cfg.AnimName))
                {
                    issues.Add(Issue(ValidationSeverity.Error, where,
                        $"Animator 里找不到状态「{cfg.AnimName}」。", cfg));
                }

                if (cfg.BaseDamage < 0 || cfg.PostureDamage < 0f)
                    issues.Add(Issue(ValidationSeverity.Error, where, "伤害为负数。", cfg));

                ValidateWindowTiming(
                    cfg.HitStartTime, cfg.RecoveryWindowStart, cfg.ComboWindowEnd, cfg.StateDuration,
                    where, cfg, issues);
                ValidatePulses(cfg.hitPulses, cfg.StateDuration, where, cfg, issues);
                ValidateCueTimes(cfg.sfxCues, cfg.StateDuration, where, "音效", cfg, issues);
                ValidateCueTimes(cfg.arrowCues, cfg.StateDuration, where, "出箭", cfg, issues);
            }
        }

        // ---------- 通用规则 ----------

        static void ValidateWindowTiming(
            float hitStart, float recoverStart, float comboEnd, float duration,
            string where, Object target, List<ValidationIssue> issues)
        {
            if (duration <= 0f)
            {
                issues.Add(Issue(ValidationSeverity.Error, where, "stateDuration <= 0。", target));
                return;
            }

            // 假红条：窗口开了一个极短的缝，本意是"关掉"实际却亮了一帧刀
            float window = recoverStart - hitStart;
            if (window > 0f && window < FakeWindowSeconds)
            {
                issues.Add(Issue(ValidationSeverity.Error, where,
                    $"判定窗口只有 {window:0.###}s（{hitStart:0.###}→{recoverStart:0.###}），这是假红条——" +
                    "第 0 帧会亮一帧刀。要关判定就把 hitStartTime/recoverStart/comboWindowEnd/stateDuration 写成相等。",
                    target));
            }

            if (window < 0f)
                issues.Add(Issue(ValidationSeverity.Error, where,
                    $"判定窗口为负（{hitStart:0.###}→{recoverStart:0.###}），判定永远不触发。", target));

            if (comboEnd < recoverStart - 0.0001f)
                issues.Add(Issue(ValidationSeverity.Error, where,
                    $"comboWindowEnd({comboEnd:0.###}) < recoverStart({recoverStart:0.###})，连招窗口在判定结束前就关了。",
                    target));

            if (comboEnd > duration + 0.0001f)
                issues.Add(Issue(ValidationSeverity.Warning, where,
                    $"comboWindowEnd({comboEnd:0.###}) 超过 stateDuration({duration:0.###})，连招窗口会被状态退出截断。",
                    target));
        }

        static void ValidatePulses(
            HitPulse[] pulses, float duration, string where, Object target, List<ValidationIssue> issues)
        {
            if (pulses == null) return;

            for (int i = 0; i < pulses.Length; i++)
            {
                HitPulse p = pulses[i];
                if (p == null) continue;
                string pWhere = $"{where} / 刀{i + 1}";

                if (p.end <= p.start)
                    issues.Add(Issue(ValidationSeverity.Error, pWhere,
                        $"脉冲区间非法（{p.start:0.###}→{p.end:0.###}）。", target));

                if (p.start < -0.0001f || p.end > duration + 0.0001f)
                    issues.Add(Issue(ValidationSeverity.Warning, pWhere,
                        $"脉冲超出动画长度（{p.start:0.###}→{p.end:0.###}，时长 {duration:0.###}）。", target));
            }
        }

        static void ValidateCueTimes(
            AttackSfxCue[] cues, float duration, string where, string label,
            Object target, List<ValidationIssue> issues)
        {
            if (cues == null) return;

            for (int i = 0; i < cues.Length; i++)
            {
                AttackSfxCue cue = cues[i];
                if (cue == null) continue;

                if (cue.time < -0.0001f || cue.time > duration + 0.0001f)
                    issues.Add(Issue(ValidationSeverity.Warning, $"{where} / {label}{i + 1}",
                        $"时间点 {cue.time:0.###}s 超出动画长度 {duration:0.###}s。", target));

                if (label == "音效" && cue.clip == null)
                    issues.Add(Issue(ValidationSeverity.Warning, $"{where} / {label}{i + 1}",
                        "没挂 AudioClip，这个点不会响。", target));
            }
        }

        static void ValidateCueTimes(
            ArrowSpawnCue[] cues, float duration, string where, string label,
            Object target, List<ValidationIssue> issues)
        {
            if (cues == null) return;

            for (int i = 0; i < cues.Length; i++)
            {
                ArrowSpawnCue cue = cues[i];
                if (cue == null) continue;

                if (cue.time < -0.0001f || cue.time > duration + 0.0001f)
                    issues.Add(Issue(ValidationSeverity.Warning, $"{where} / {label}{i + 1}",
                        $"时间点 {cue.time:0.###}s 超出动画长度 {duration:0.###}s。", target));
            }
        }

        // ---------- Animator 状态名索引 ----------

        // 返回 null 表示 controller 读不到，调用方按"跳过这项校验"处理
        static HashSet<string> CollectStateNames(string controllerPath)
        {
            AnimatorController ac = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (ac == null) return null;

            HashSet<string> names = new HashSet<string>();
            for (int i = 0; i < ac.layers.Length; i++)
            {
                if (ac.layers[i].stateMachine == null) continue;
                CollectFromMachine(ac.layers[i].stateMachine, names);
            }
            return names;
        }

        static void CollectFromMachine(AnimatorStateMachine machine, HashSet<string> names)
        {
            if (machine == null) return;

            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null) names.Add(states[i].state.name);
            }

            ChildAnimatorStateMachine[] children = machine.stateMachines;
            for (int i = 0; i < children.Length; i++)
                CollectFromMachine(children[i].stateMachine, names);
        }

        static ValidationIssue Issue(ValidationSeverity severity, string where, string message, Object target)
        {
            return new ValidationIssue
            {
                Severity = severity,
                Where = where,
                Message = message,
                Target = target,
            };
        }

        public static int CountErrors(List<ValidationIssue> issues)
        {
            int n = 0;
            for (int i = 0; i < issues.Count; i++)
                if (issues[i].Severity == ValidationSeverity.Error) n++;
            return n;
        }

        public static string ToReport(List<ValidationIssue> issues)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"共 {issues.Count} 条（错误 {CountErrors(issues)} 条）");
            for (int i = 0; i < issues.Count; i++)
                sb.AppendLine("  " + issues[i]);
            return sb.ToString();
        }
    }

}
