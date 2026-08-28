using UnityEngine;
using UnityEngine.InputSystem;

// MVC 之 Controller：订阅 CombatEventBus，把战斗数值转给对应 View
// 只做"数据 → View"的转发，不持有业务逻辑、不做每帧轮询（表现层红线）
public class CombatUIController : MonoBehaviour
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

    private void Awake()
    {
        // 旧 HUD 没挂组件也能播台词，不必手改预制体
        if (GetComponent<BossVoiceDirector>() == null)
            gameObject.AddComponent<BossVoiceDirector>();
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
        BindLockOnView();
        BindPerilousView();
        BindHealKanjiView();
        BindReviveKanjiView();
        revivePromptView?.OnViewInit();
        gameOverView?.OnViewInit();
        victoryView?.OnViewInit();

        BossVoiceDirector voice = GetComponent<BossVoiceDirector>();
        voice?.Bind(playerBody, bossBody, playerPostureBarView != null ? playerPostureBarView.transform as RectTransform : null);

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
            BindHealKanjiView();
        healKanjiView?.BindFollowTarget(playerBody);
        healKanjiView?.ShowHeal();
    }

    private void HandleReviveAvailable(CharacterBody c)
    {
        if (c == playerBody)
        {
            // 这次死亡会用掉一次回生：立刻把对应活点换成 EndDot
            playerStatusView?.SetReviveDots(Mathf.Max(0, playerBody.ReviveRemaining - 1));
            revivePromptView?.BeginDeathFade(playerBody != null ? playerBody.GetComponent<PlayerInput>() : null);
        }
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
                BindReviveKanjiView();
            reviveKanjiView?.BindFollowTarget(playerBody);
            reviveKanjiView?.ShowRevive();
        }
    }

    private void HandleDeath(CharacterBody c)
    {
        if (c == playerBody)
        {
            revivePromptView?.HidePrompt();
            playerStatusView?.SetReviveDots(0);
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
            BindPerilousView();
        perilousWarningView?.BindFollowTarget(playerBody);
        perilousWarningView?.ShowWarning(type);
    }

    private void HandleLockOnChanged(bool isLocked)
    {
        if (lockOnIndicatorView == null)
            BindLockOnView();
        lockOnIndicatorView?.SetLocked(isLocked);
    }

    // 场景里引用常被清空/脚本被关掉，运行时自己找并打开
    private void BindLockOnView()
    {
        if (lockOnIndicatorView == null)
        {
            GameObject named = GameObject.Find("LockOnIndicator");
            if (named != null)
                lockOnIndicatorView = named.GetComponent<LockOnIndicatorView>();
        }

        if (lockOnIndicatorView == null)
        {
            LockOnIndicatorView[] views = FindObjectsOfType<LockOnIndicatorView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.scene.IsValid())
                {
                    lockOnIndicatorView = views[i];
                    break;
                }
            }
        }

        if (lockOnIndicatorView == null) return;
        lockOnIndicatorView.enabled = true;
        lockOnIndicatorView.gameObject.SetActive(true);
        lockOnIndicatorView.BindFollowTarget(bossBody);
        lockOnIndicatorView.OnViewInit();
    }

    private void BindPerilousView()
    {
        if (perilousWarningView == null)
        {
            GameObject named = GameObject.Find("PerilousWarning");
            if (named != null)
                perilousWarningView = named.GetComponent<PerilousWarningView>();
        }

        if (perilousWarningView == null)
        {
            PerilousWarningView[] views = FindObjectsOfType<PerilousWarningView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.scene.IsValid())
                {
                    perilousWarningView = views[i];
                    break;
                }
            }
        }

        if (perilousWarningView == null) return;
        perilousWarningView.enabled = true;
        perilousWarningView.BindFollowTarget(playerBody);
        perilousWarningView.OnViewInit();
    }

    private void BindHealKanjiView()
    {
        if (healKanjiView == null)
        {
            GameObject named = GameObject.Find("HealKanji");
            if (named != null)
                healKanjiView = named.GetComponent<HealKanjiView>();
        }

        if (healKanjiView == null)
        {
            HealKanjiView[] views = FindObjectsOfType<HealKanjiView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.scene.IsValid())
                {
                    healKanjiView = views[i];
                    break;
                }
            }
        }

        if (healKanjiView == null) return;
        healKanjiView.enabled = true;
        healKanjiView.BindFollowTarget(playerBody);
        healKanjiView.OnViewInit();
    }

    private void BindReviveKanjiView()
    {
        if (reviveKanjiView == null)
        {
            GameObject named = GameObject.Find("ReviveKanji");
            if (named != null)
                reviveKanjiView = named.GetComponent<ReviveKanjiView>();
        }

        if (reviveKanjiView == null)
        {
            ReviveKanjiView[] views = FindObjectsOfType<ReviveKanjiView>(true);
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i] != null && views[i].gameObject.scene.IsValid())
                {
                    reviveKanjiView = views[i];
                    break;
                }
            }
        }

        if (reviveKanjiView == null) return;
        reviveKanjiView.enabled = true;
        reviveKanjiView.BindFollowTarget(playerBody);
        reviveKanjiView.OnViewInit();
    }
}
