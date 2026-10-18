using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

using ARPG.Audio;
using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.UI
{

    // 订阅战斗事件并更新 HUD，同时在复战时恢复提示界面和玩家输入。
    public class CombatUIController : MonoBehaviour, ICombatResettable
    {
        [Header("角色引用（用于区分玩家/Boss）")]
        [SerializeField] private CharacterBody playerBody;
        [SerializeField] private CharacterBody bossBody;

        [Header("View 引用")]
        [SerializeField] private BossStatusView bossStatusView;        // 左上：红点+血条+名称
        [SerializeField] private BossPostureBarView bossPostureBarView;   // 顶部：Boss 架势条
        [SerializeField] private BossPostureBarView playerPostureBarView; // 底栏正中：玩家架势条（物体名必须是 PlayerPosture）
        [SerializeField] private PlayerStatusView playerStatusView;       // 左下：玩家血条+回生
        [SerializeField] private ItemSlotView itemSlotView;             // 右下：葫芦
        [SerializeField] private LockOnIndicatorView lockOnIndicatorView; // 屏幕锁定点，跟随 Boss Spine1
        [SerializeField] private PerilousWarningView perilousWarningView; // "危"字
        [SerializeField] private HealKanjiView healKanjiView;           // "治"字
        [SerializeField] private ReviveKanjiView reviveKanjiView;       // "回生"字
        [SerializeField] private RevivePromptView revivePromptView;     // 回生提示
        [SerializeField] private GameOverView gameOverView;             // 死亡提示
        [SerializeField] private VictoryView victoryView;               // 胜利提示

        // 台词界面由 BossVoiceDirector 独立管理。
        private BossVoiceDirector voiceDirector;

        // 手工登记跟随视图，避免漏掉已移出 HUD 层级的世界空间物体。
        private readonly List<UIView> managedViews = new List<UIView>();

        private void Awake()
        {
            // 复用或创建台词控制器，统一管理字幕和语音。
            voiceDirector = GetComponent<BossVoiceDirector>();
            if (voiceDirector == null)
                voiceDirector = gameObject.AddComponent<BossVoiceDirector>();
        }

        // ===== 生命周期：订阅 / 取消订阅 =====
        private void OnEnable()
        {
            CombatEventBus.OnHPChanged += HandleHPChanged;
            CombatEventBus.OnPostureChanged += HandlePostureChanged;
            CombatEventBus.OnPostureBroken += HandlePostureBroken;
            CombatEventBus.OnFinisherOpportunityChanged += HandleFinisherOpportunityChanged;
            CombatEventBus.OnGourdUsed += HandleGourdUsed;
            CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
            CombatEventBus.OnReviveChoiceReady += HandleReviveChoiceReady;
            CombatEventBus.OnRevived += HandleRevived;
            CombatEventBus.OnDeath += HandleDeath;
            CombatEventBus.OnVictory += HandleVictory;
            CombatEventBus.OnLifeCleared += HandleLifeCleared;
            CombatEventBus.OnPerilousAttack += HandlePerilousAttack;
            CombatEventBus.OnLockOnChanged += HandleLockOnChanged;
        }

        private void OnDisable()
        {
            CombatEventBus.OnHPChanged -= HandleHPChanged;
            CombatEventBus.OnPostureChanged -= HandlePostureChanged;
            CombatEventBus.OnPostureBroken -= HandlePostureBroken;
            CombatEventBus.OnFinisherOpportunityChanged -= HandleFinisherOpportunityChanged;
            CombatEventBus.OnGourdUsed -= HandleGourdUsed;
            CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
            CombatEventBus.OnReviveChoiceReady -= HandleReviveChoiceReady;
            CombatEventBus.OnRevived -= HandleRevived;
            CombatEventBus.OnDeath -= HandleDeath;
            CombatEventBus.OnVictory -= HandleVictory;
            CombatEventBus.OnLifeCleared -= HandleLifeCleared;
            CombatEventBus.OnPerilousAttack -= HandlePerilousAttack;
            CombatEventBus.OnLockOnChanged -= HandleLockOnChanged;
        }

        private void Start()
        {
            // 初始化常驻 HUD，并为世界空间提示绑定角色。
            bossStatusView.OnViewInit();
            bossPostureBarView.OnViewInit();
            playerPostureBarView.OnViewInit();
            playerStatusView.OnViewInit();
            itemSlotView.OnViewInit();
            BindFollowView(lockOnIndicatorView, bossBody);
            BindFollowView(perilousWarningView, playerBody);
            BindFollowView(healKanjiView, playerBody);
            BindFollowView(reviveKanjiView, playerBody);
            revivePromptView.OnViewInit();
            gameOverView?.OnViewInit();
            victoryView.OnViewInit();

            // 登记已初始化的视图，供复战时统一复位。
            CollectManagedViews();

            voiceDirector.Bind(playerBody, bossBody, (RectTransform)playerPostureBarView.transform);

            // 角色初始化不会发送数值事件，开局需主动推送血条、命数和物品数量。
            bossStatusView.SetName("苇名弦一郎");
            bossStatusView.SetLifeDots(bossBody.Config.LifeCount);
            playerStatusView.SetReviveDots(playerBody.Config.ReviveCount);
            PushCurrentStats(playerBody, isPlayer: true);
            PushCurrentStats(bossBody, isPlayer: false);
            itemSlotView.SetGourdCount(playerBody.GourdRemaining);

            // 提示类视图初始隐藏
            revivePromptView.Hide();
            gameOverView?.Hide();
            victoryView.Hide();

            EncounterScope.Ensure()?.Register(this);
        }

        private void OnDestroy()
        {
            EncounterScope.Current?.Unregister(this);
        }

        private void PushCurrentStats(CharacterBody c, bool isPlayer)
        {
            int maxHp = c.Config.MaxHP;
            float hpRatio = (float)c.CurrentHP / maxHp;
            float maxPosture = c.Config.MaxPosture;
            float postureRatio = c.CurrentPosture / maxPosture;
            if (isPlayer)
            {
                playerStatusView.SetHP(hpRatio);
                playerPostureBarView.SetPosture(postureRatio);
            }
            else
            {
                bossStatusView.SetHP(hpRatio);
                bossPostureBarView.SetPosture(postureRatio);
            }
        }

        // 清理上一场的界面与输入状态；初始数值从 Config 读取，避免依赖各组件的复位顺序。
        public void ResetForEncounter()
        {
            // 先让视图清理 tween、计时器和跟随目标，再恢复战斗初始显示。
            for (int i = 0; i < managedViews.Count; i++)
            {
                if (managedViews[i] == null) continue;
                managedViews[i].ResetForEncounter();
            }
            voiceDirector.ResetForEncounter();

            CombatInputGate.SetBlocked(false);
            lockOnIndicatorView.SetFinisherReady(false);

            // 恢复胜利时关闭的输入；暂停期间仍由菜单持有设备。
            if (!GamePause.IsPaused)
            {
                PlayerInput input = playerBody.GetComponent<PlayerInput>();
                input.ActivateInput();
            }

            playerStatusView.SetReviveDots(playerBody.Config.ReviveCount);
            itemSlotView.SetGourdCount(playerBody.Config.GourdCount);
            bossStatusView.SetLifeDots(bossBody.Config.LifeCount);

            // 视图复位已清空目标，此时重新绑定当前 Boss。
            lockOnIndicatorView.BindFollowTarget(bossBody);
        }

        // 集中登记需要复战复位的视图；新增受管视图时同步此列表。
        private void CollectManagedViews()
        {
            managedViews.Clear();
            AddManaged(bossStatusView);
            AddManaged(bossPostureBarView);
            AddManaged(playerPostureBarView);
            AddManaged(playerStatusView);
            AddManaged(itemSlotView);
            AddManaged(lockOnIndicatorView);
            AddManaged(perilousWarningView);
            AddManaged(healKanjiView);
            AddManaged(reviveKanjiView);
            AddManaged(revivePromptView);
            if (gameOverView != null) AddManaged(gameOverView);
            AddManaged(victoryView);
        }

        private void AddManaged(UIView view)
        {
            if (managedViews.Contains(view)) return;
            managedViews.Add(view);
        }

        // 供流程层切换玩家或 Boss 引用，并更新跟随目标；传入空值表示保留原角色。
        public void Rebind(CharacterBody player, CharacterBody boss)
        {
            if (player != null) playerBody = player;
            if (boss != null) bossBody = boss;

            if (bossBody != null)
                BindFollowView(lockOnIndicatorView, bossBody);
            if (playerBody != null)
            {
                BindFollowView(perilousWarningView, playerBody);
                BindFollowView(healKanjiView, playerBody);
                BindFollowView(reviveKanjiView, playerBody);
            }
            CollectManagedViews();
        }

        // Boss 名称由流程层提供，CharacterConfig 不保存显示名称。
        public void SetBossName(string name)
        {
            bossStatusView.SetName(name);
        }

        // ===== 事件处理 =====

        private void HandleHPChanged(CharacterBody c, int currentHp, int maxHp)
        {
            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
            if (c == playerBody)
            {
                playerStatusView.SetHP(ratio);
            }
            else if (c == bossBody)
            {
                bossStatusView.SetHP(ratio);
            }
        }

        private void HandlePostureChanged(CharacterBody c, float posture, float maxPosture)
        {
            float ratio = maxPosture > 0f ? posture / maxPosture : 0f;
            if (c == playerBody)
            {
                playerPostureBarView.SetPosture(ratio);
            }
            else if (c == bossBody)
            {
                bossPostureBarView.SetPosture(ratio);
                // 崩解结束架势归零 → 处决红点熄灭
                if (ratio <= 0.001f)
                {
                    lockOnIndicatorView.SetFinisherReady(false);
                }
            }
        }

        private void HandlePostureBroken(CharacterBody c)
        {
            if (c == bossBody)
            {
                // Boss 架势崩解 → 处决窗口 → 锁定点变红
                lockOnIndicatorView.SetFinisherReady(true);
            }
        }

        private void HandleFinisherOpportunityChanged(CharacterBody target, bool available)
        {
            if (target == bossBody)
            {
                lockOnIndicatorView.SetFinisherReady(available);
            }
        }

        private void HandleGourdUsed(CharacterBody c, int remaining)
        {
            if (c != playerBody) return;
            itemSlotView.SetGourdCount(remaining);
            healKanjiView.BindFollowTarget(playerBody);
            healKanjiView.ShowHeal();
        }

        private void HandleReviveAvailable(CharacterBody c)
        {
            if (c == playerBody)
                revivePromptView.BeginDeathFade(playerBody.GetComponent<PlayerInput>());
        }

        private void HandleReviveChoiceReady(CharacterBody c)
        {
            if (c == playerBody)
            {
                revivePromptView.ShowChoices(
                    // 系统指令：UI 选"就此死去"不伪造物理按下身份，不开启弹反窗口、不计空按。
                    () => playerBody.TryExecuteCommand(DeflectCommand.System()),
                    () => playerBody.TryExecuteCommand(new AttackCommand()));
            }
        }

        private void HandleRevived(CharacterBody c)
        {
            if (c == playerBody)
            {
                revivePromptView.HidePrompt();
                playerStatusView.SetReviveDots(playerBody.ReviveRemaining);
                reviveKanjiView.BindFollowTarget(playerBody);
                reviveKanjiView.ShowRevive();
            }
        }

        private void HandleDeath(CharacterBody c)
        {
            if (c == playerBody)
            {
                revivePromptView.HidePrompt();
                playerStatusView.SetReviveDots(playerBody.ReviveRemaining);
                gameOverView?.ShowGameOver();
            }
        }

        private void HandleVictory(CharacterBody c)
        {
            if (c != bossBody) return;

            CombatInputGate.SetBlocked(true);
            FreezePlayerMotion();
            ReleasePlayerDevicesForUi();
            victoryView.ShowVictory();
        }

        // 清除移动意图和水平速度，避免胜利后玩家继续滑动。
        private void FreezePlayerMotion()
        {
            playerBody.MoveDirection = Vector3.zero;
            Vector3 velocity = playerBody.Rb.velocity;
            playerBody.Rb.velocity = new Vector3(0f, velocity.y, 0f);
        }

        // 释放 PlayerInput 独占的设备，让胜利界面收到手柄输入。
        private void ReleasePlayerDevicesForUi()
        {
            PlayerInput input = playerBody.GetComponent<PlayerInput>();
            input.DeactivateInput();
        }

        private void HandleLifeCleared(CharacterBody c, int remainingLives)
        {
            if (c == bossBody)
            {
                bossStatusView.SetLifeDots(remainingLives);
                lockOnIndicatorView.SetFinisherReady(false);
            }
        }

        private void HandlePerilousAttack(PerilousType type)
        {
            perilousWarningView.BindFollowTarget(playerBody);
            perilousWarningView.ShowWarning();
        }

        private void HandleLockOnChanged(bool isLocked)
        {
            lockOnIndicatorView.SetLocked(isLocked);
        }

        // 跟随视图由场景引用指定；初始化负责启用和绑定目标。
        private static void BindFollowView<T>(T view, CharacterBody target)
            where T : UIView, IFollowTargetView
        {
            view.enabled = true;
            view.gameObject.SetActive(true);
            view.BindFollowTarget(target);
            view.OnViewInit();
        }
    }

}
