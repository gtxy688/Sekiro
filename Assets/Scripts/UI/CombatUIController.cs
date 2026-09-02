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

    // MVC 之 Controller：订阅 CombatEventBus，把战斗数值转给对应 View
    // 只做"数据 → View"的转发，不持有业务逻辑、不做每帧轮询（表现层红线）
    //
    // 实现 ICombatResettable：胜利 / 死亡 / 回生这些提示会顺手改全局开关
    // （CombatInputGate.SetBlocked、PlayerInput.DeactivateInput），
    // 而它们只在"重开场景"那条路径上被还原。复战不重载场景，不还原就是开局不能动。
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
        [SerializeField] private RevivePromptView revivePromptView;     // 回生提示（M14）
        [SerializeField] private GameOverView gameOverView;             // 死亡提示（M14）
        [SerializeField] private VictoryView victoryView;               // 胜利提示（M10）

        // 台词不归 Controller 管：VoiceLineView 是 BossVoiceDirector 的私有引用，
        // 这里再持一份就变成两个组件抢同一个 View，改哪边都会觉得另一边是脏的。
        private BossVoiceDirector voiceDirector;

        // 连战重置要遍历的 View。手工登记而不是 GetComponentsInChildren，
        // 是因为锁定点被搬到世界空间、三个汉字挂在各自的物体下，
        // 它们根本不在本 Controller 的子层级里，靠层级查找一个都捞不着。
        private readonly List<UIView> managedViews = new List<UIView>();

        private void Awake()
        {
            // 旧 HUD 没挂组件也能播台词，不必手改预制体
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
            // 初始化各 View 的引用（替换 Awake 手动查找）
            bossStatusView?.OnViewInit();
            bossPostureBarView?.OnViewInit();
            playerPostureBarView?.OnViewInit();
            playerStatusView?.OnViewInit();
            itemSlotView?.OnViewInit();
            BindFollowView(ref lockOnIndicatorView, "LockOnIndicator", bossBody);
            BindFollowView(ref perilousWarningView, "PerilousWarning", playerBody);
            BindFollowView(ref healKanjiView, "HealKanji", playerBody);
            BindFollowView(ref reviveKanjiView, "ReviveKanji", playerBody);
            revivePromptView?.OnViewInit();
            gameOverView?.OnViewInit();
            victoryView?.OnViewInit();

            // 登记放在所有 Bind 之后：Bind 可能刚把场景里找回来的 View 填进字段，
            // 先登记就会漏掉这些"迟到"的引用。
            CollectManagedViews();

            voiceDirector?.Bind(playerBody, bossBody, playerPostureBarView != null ? playerPostureBarView.transform as RectTransform : null);

            // 初始状态（Awake 里 InitCombat 不会发事件，这里把当前数值推到条上，否则开局血条/葫芦是空的）
            bossStatusView?.SetName("苇名弦一郎");
            bossStatusView?.SetLifeDots(bossBody != null && bossBody.Config != null ? bossBody.Config.LifeCount : 2);
            playerStatusView?.SetReviveDots(playerBody != null && playerBody.Config != null ? playerBody.Config.ReviveCount : 1);
            PushCurrentStats(playerBody, isPlayer: true);
            PushCurrentStats(bossBody, isPlayer: false);
            if (playerBody != null)
                itemSlotView?.SetGourdCount(playerBody.GourdRemaining);

            // 提示类视图初始隐藏
            revivePromptView?.Hide();
            gameOverView?.Hide();
            victoryView?.Hide();

            EncounterScope.Ensure()?.Register(this);
        }

        private void OnDestroy()
        {
            EncounterScope.Current?.Unregister(this);
        }

        private void PushCurrentStats(CharacterBody c, bool isPlayer)
        {
            if (c == null) return;
            int maxHp = c.Config != null ? c.Config.MaxHP : 0;
            float hpRatio = maxHp > 0 ? (float)c.CurrentHP / maxHp : 0f;
            float maxPosture = c.Config != null ? c.Config.MaxPosture : 100f;
            float postureRatio = maxPosture > 0f ? c.CurrentPosture / maxPosture : 0f;
            if (isPlayer)
            {
                playerStatusView?.SetHP(hpRatio);
                playerPostureBarView?.SetPosture(postureRatio);
            }
            else
            {
                bossStatusView?.SetHP(hpRatio);
                bossPostureBarView?.SetPosture(postureRatio);
            }
        }

        // 复战重置：把"只由事件驱动"的提示 UI 与全局开关拉回开局状态。
        //
        // 这些状态不跟着 CharacterBody 的复位一起回来，因为它们的来源根本不是数值：
        //   - 胜利时打了 CombatInputGate.SetBlocked(true) 并 DeactivateInput()，
        //     原本只靠 VictoryView 的"重开场景"按钮还原。复战不重载场景，
        //     不还原就是开局人物完全不能动——这是这一批里最致命的一项。
        //   - 回生提示 / Game Over 只由"回生成功"或"真死"关闭，复战两条都不走。
        //   - 命数点 / 回生点只由 OnLifeCleared、OnRevived 刷新，复战不重发这两个事件。
        //
        // 命数点与回生点刻意从 Config 读、不从 body 的当前值读：
        // ResetAll() 里各组件的执行顺序未定义，UI 可能先于 CharacterBody 复位，
        // 那时 body 上的还是上一场的残值。Config 是序列化 SO，整局不变，读它没有顺序依赖。
        public void ResetForEncounter()
        {
            // 分两步，职责不同：
            //   第一步让每个 View 清掉自己的临时状态（tween、倒计时、跟随目标、一次性标志）。
            //   第二步由本方法把战斗初始值推一遍。
            //
            // 这两类东西不能混：View 清的是"上一场的历史"，没有外部数据源，只能自己清；
            // 而这里推的值必须从 Config 读——各组件复位顺序未定义，
            // 读 body 的当前值可能读到还没复位的残值。
            for (int i = 0; i < managedViews.Count; i++)
            {
                if (managedViews[i] == null) continue;
                managedViews[i].ResetForEncounter();
            }
            voiceDirector?.ResetForEncounter();

            CombatInputGate.SetBlocked(false);
            lockOnIndicatorView?.SetFinisherReady(false);

            // 胜利时被 DeactivateInput() 关掉的输入要重新打开。
            // 暂停中不动它：那时输入归暂停菜单，抢回来会把菜单卡死。
            if (playerBody != null && !GamePause.IsPaused)
            {
                PlayerInput input = playerBody.GetComponent<PlayerInput>();
                input?.ActivateInput();
            }

            if (playerBody != null)
            {
                playerStatusView?.SetReviveDots(playerBody.Config != null ? playerBody.Config.ReviveCount : 1);
                itemSlotView?.SetGourdCount(playerBody.Config != null ? playerBody.Config.GourdCount : 0);
            }
            if (bossBody != null)
                bossStatusView?.SetLifeDots(bossBody.Config != null ? bossBody.Config.LifeCount : 2);

            // 锁定点在 View 自己的 Reset 里清掉了跟随目标，这里按当前 bossBody 重新钉上。
            // 放在数值推送之后，保证连战换 Boss 时它钉的是新 Boss 而不是上一场那个。
            if (lockOnIndicatorView != null)
                lockOnIndicatorView.BindFollowTarget(bossBody);
        }

        // 登记所有托管的 View。新增 View 时在这里加一行——
        // 这是唯一需要同步的地方，比让每个 View 各自去 EncounterScope 注册容易核对得多。
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
            AddManaged(gameOverView);
            AddManaged(victoryView);
        }

        private void AddManaged(UIView view)
        {
            if (view == null) return;
            if (managedViews.Contains(view)) return;
            managedViews.Add(view);
        }

        // 连战接缝：换 Boss / 换玩家时由流程层调用。当前无人调用。
        //
        // 为什么现在就留：HandleHPChanged 这一串判断全靠 `c == bossBody` 比对身份。
        // 连战不重载场景，bossBody 这个序列化引用不会自己指向新 Boss；
        // 引用不更新 = 新 Boss 的所有事件被静默丢弃，血条一动不动且不报错。
        // 这类"不报错的静默失效"正是连战最难查的一类，入口提前留在这，
        // 流程层接进来时不必再翻一遍本类的字段去猜该改哪几处。
        public void Rebind(CharacterBody player, CharacterBody boss)
        {
            if (player != null) playerBody = player;
            if (boss != null) bossBody = boss;

            if (bossBody != null)
                BindFollowView(ref lockOnIndicatorView, "LockOnIndicator", bossBody);
            if (playerBody != null)
            {
                BindFollowView(ref perilousWarningView, "PerilousWarning", playerBody);
                BindFollowView(ref healKanjiView, "HealKanji", playerBody);
                BindFollowView(ref reviveKanjiView, "ReviveKanji", playerBody);
            }
            CollectManagedViews();
        }

        // Boss 名没有数据源：CharacterConfig 里没有任何名字字段，
        // 所以连战换 Boss 时由流程层自己传。这里不臆造 config.BossName——
        // 名字该挂在哪（Config / Boss 定义 SO / 流程层写死）是连战设计时才说得清的事。
        public void SetBossName(string name)
        {
            bossStatusView?.SetName(name);
        }

        // ===== 事件处理 =====

        private void HandleHPChanged(CharacterBody c, int currentHp, int maxHp)
        {
            float ratio = maxHp > 0 ? (float)currentHp / maxHp : 0f;
            if (c == playerBody)
            {
                playerStatusView?.SetHP(ratio);
            }
            else if (c == bossBody)
            {
                bossStatusView?.SetHP(ratio);
            }
        }

        private void HandlePostureChanged(CharacterBody c, float posture, float maxPosture)
        {
            float ratio = maxPosture > 0f ? posture / maxPosture : 0f;
            if (c == playerBody)
            {
                playerPostureBarView?.SetPosture(ratio);
            }
            else if (c == bossBody)
            {
                bossPostureBarView?.SetPosture(ratio);
                // 崩解结束架势归零 → 处决红点熄灭
                if (ratio <= 0.001f)
                {
                    lockOnIndicatorView?.SetFinisherReady(false);
                }
            }
        }

        private void HandlePostureBroken(CharacterBody c)
        {
            if (c == bossBody)
            {
                // Boss 架势崩解 → 处决窗口 → 锁定点变红
                lockOnIndicatorView?.SetFinisherReady(true);
            }
        }

        private void HandleFinisherOpportunityChanged(CharacterBody target, bool available)
        {
            if (target == bossBody)
            {
                lockOnIndicatorView?.SetFinisherReady(available);
            }
        }

        private void HandleGourdUsed(CharacterBody c, int remaining)
        {
            if (c != playerBody) return;
            itemSlotView?.SetGourdCount(remaining);
            if (healKanjiView == null)
                BindFollowView(ref healKanjiView, "HealKanji", playerBody);
            healKanjiView?.BindFollowTarget(playerBody);
            healKanjiView?.ShowHeal();
        }

        private void HandleReviveAvailable(CharacterBody c)
        {
            if (c == playerBody)
                revivePromptView?.BeginDeathFade(playerBody != null ? playerBody.GetComponent<PlayerInput>() : null);
        }

        private void HandleReviveChoiceReady(CharacterBody c)
        {
            if (c == playerBody)
            {
                revivePromptView?.ShowChoices(
                    () => playerBody.TryExecuteCommand(new DeflectCommand()),
                    () => playerBody.TryExecuteCommand(new AttackCommand()));
            }
        }

        private void HandleRevived(CharacterBody c)
        {
            if (c == playerBody)
            {
                revivePromptView?.HidePrompt();
                playerStatusView?.SetReviveDots(playerBody.ReviveRemaining);
                if (reviveKanjiView == null)
                    BindFollowView(ref reviveKanjiView, "ReviveKanji", playerBody);
                reviveKanjiView?.BindFollowTarget(playerBody);
                reviveKanjiView?.ShowRevive();
            }
        }

        private void HandleDeath(CharacterBody c)
        {
            if (c == playerBody)
            {
                revivePromptView?.HidePrompt();
                playerStatusView?.SetReviveDots(playerBody != null ? playerBody.ReviveRemaining : 0);
                gameOverView?.ShowGameOver();
            }
        }

        private void HandleVictory(CharacterBody c)
        {
            if (c != bossBody) return;

            CombatInputGate.SetBlocked(true);
            FreezePlayerMotion();
            ReleasePlayerDevicesForUi();
            victoryView?.ShowVictory();
        }

        // 处决结束会回待机，不把摇杆关掉的话还能接着走
        private void FreezePlayerMotion()
        {
            if (playerBody == null) return;
            playerBody.MoveDirection = Vector3.zero;
            if (playerBody.Rb == null) return;
            Vector3 velocity = playerBody.Rb.velocity;
            playerBody.Rb.velocity = new Vector3(0f, velocity.y, 0f);
        }

        // 手柄被 PlayerInput 独占时，胜利按钮收不到确认
        private void ReleasePlayerDevicesForUi()
        {
            if (playerBody == null) return;
            PlayerInput input = playerBody.GetComponent<PlayerInput>();
            if (input != null)
                input.DeactivateInput();
        }

        private void HandleLifeCleared(CharacterBody c, int remainingLives)
        {
            if (c == bossBody)
            {
                bossStatusView?.SetLifeDots(remainingLives);
                lockOnIndicatorView?.SetFinisherReady(false);
            }
        }

        private void HandlePerilousAttack(PerilousType type)
        {
            if (perilousWarningView == null)
                BindFollowView(ref perilousWarningView, "PerilousWarning", playerBody);
            perilousWarningView?.BindFollowTarget(playerBody);
            perilousWarningView?.ShowWarning();
        }

        private void HandleLockOnChanged(bool isLocked)
        {
            if (lockOnIndicatorView == null)
                BindFollowView(ref lockOnIndicatorView, "LockOnIndicator", bossBody);
            lockOnIndicatorView?.SetLocked(isLocked);
        }

        // 场景里引用常被清空（脚本被关掉、Prefab 覆盖丢失、合并场景丢引用），
        // 所以跟随型 View 都要能在事件首次到达时自己找回来并打开。
        //
        // 这四个 Bind 方法原本逐行相同，只差类型名与物体名。
        // 公共上界刻意用 IFollowTargetView 而不是 UIView：UIView 太宽，
        // 传进来一个不跟随的 View（比如血条）编译器也只会默许，
        // 到运行时才发现它根本没有 BindFollowTarget。
        private void BindFollowView<T>(ref T view, string objectName, CharacterBody target)
            where T : UIView, IFollowTargetView
        {
            if (view == null)
            {
                GameObject named = GameObject.Find(objectName);
                if (named != null)
                    view = named.GetComponent<T>();
            }

            if (view == null)
            {
                T[] found = FindObjectsOfType<T>(true);
                for (int i = 0; i < found.Length; i++)
                {
                    // 只认场景里的实例，跳过 Prefab 资源里那份
                    if (found[i] != null && found[i].gameObject.scene.IsValid())
                    {
                        view = found[i];
                        break;
                    }
                }
            }

            if (view == null) return;
            view.enabled = true;
            view.gameObject.SetActive(true);
            view.BindFollowTarget(target);
            view.OnViewInit();
        }
    }

}
