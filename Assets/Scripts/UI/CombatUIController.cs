using UnityEngine;

// MVC 之 Controller：订阅 CombatEventBus，把战斗数值转给对应 View
// 只做"数据 → View"的转发，不持有业务逻辑、不做每帧轮询（表现层红线）
public class CombatUIController : MonoBehaviour
{
    [Header("角色引用（用于区分玩家/Boss）")]
    [SerializeField] private CharacterBody playerBody;
    [SerializeField] private CharacterBody bossBody;

    [Header("View 引用")]
    [SerializeField] private BossStatusView bossStatusView;        // 左上：红点+血条+名称
    [SerializeField] private BossPostureBarView bossPostureBarView; // 顶部：Boss 架势条
    [SerializeField] private PlayerStatusView playerStatusView;     // 左下：玩家血条+架势+回生
    [SerializeField] private ItemSlotView itemSlotView;             // 右下：葫芦
    [SerializeField] private LockOnIndicatorView lockOnIndicatorView; // Boss 身上锁定点
    [SerializeField] private PerilousWarningView perilousWarningView; // "危"字
    [SerializeField] private RevivePromptView revivePromptView;     // 回生提示（M14）
    [SerializeField] private GameOverView gameOverView;             // 死亡提示（M14）
    [SerializeField] private VictoryView victoryView;               // 胜利提示（M10）

    // ===== 生命周期：订阅 / 取消订阅 =====
    private void OnEnable()
    {
        CombatEventBus.OnTakeDamage += HandleTakeDamage;
        CombatEventBus.OnHPChanged += HandleHPChanged;
        CombatEventBus.OnPostureChanged += HandlePostureChanged;
        CombatEventBus.OnPostureBroken += HandlePostureBroken;
        CombatEventBus.OnGourdUsed += HandleGourdUsed;
        CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
        CombatEventBus.OnRevived += HandleRevived;
        CombatEventBus.OnDeath += HandleDeath;
        CombatEventBus.OnVictory += HandleVictory;
        CombatEventBus.OnLifeCleared += HandleLifeCleared;
        CombatEventBus.OnPerilousAttack += HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered += HandleFinisherTriggered;
        CombatEventBus.OnLockOnChanged += HandleLockOnChanged;
    }

    private void OnDisable()
    {
        CombatEventBus.OnTakeDamage -= HandleTakeDamage;
        CombatEventBus.OnHPChanged -= HandleHPChanged;
        CombatEventBus.OnPostureChanged -= HandlePostureChanged;
        CombatEventBus.OnPostureBroken -= HandlePostureBroken;
        CombatEventBus.OnGourdUsed -= HandleGourdUsed;
        CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
        CombatEventBus.OnRevived -= HandleRevived;
        CombatEventBus.OnDeath -= HandleDeath;
        CombatEventBus.OnVictory -= HandleVictory;
        CombatEventBus.OnLifeCleared -= HandleLifeCleared;
        CombatEventBus.OnPerilousAttack -= HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered -= HandleFinisherTriggered;
        CombatEventBus.OnLockOnChanged -= HandleLockOnChanged;
    }

    private void Start()
    {
        // 初始化各 View 的引用（替换 Awake 手动查找）
        bossStatusView?.OnViewInit();
        bossPostureBarView?.OnViewInit();
        playerStatusView?.OnViewInit();
        itemSlotView?.OnViewInit();
        lockOnIndicatorView?.OnViewInit();
        perilousWarningView?.OnViewInit();
        revivePromptView?.OnViewInit();
        gameOverView?.OnViewInit();
        victoryView?.OnViewInit();

        // 初始状态
        bossStatusView?.SetName("苇名弦一郎");
        bossStatusView?.SetLifeDots(bossBody != null && bossBody.Config != null ? bossBody.Config.LifeCount : 2);
        playerStatusView?.SetReviveDots(playerBody != null && playerBody.Config != null ? playerBody.Config.ReviveCount : 1);

        // 提示类视图初始隐藏
        revivePromptView?.Hide();
        gameOverView?.Hide();
        victoryView?.Hide();
    }

    // ===== 事件处理 =====

    // 血条：根据 victim 判断是玩家还是 Boss，转发给对应 View
    private void HandleTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        // 老事件（无 maxHp），用 OnHPChanged 那个带完整数据的
    }

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
            playerStatusView?.SetPosture(ratio);
            playerStatusView?.SetDanger(ratio > 0.8f);
        }
        else if (c == bossBody)
        {
            bossPostureBarView?.SetPosture(ratio);
            bossPostureBarView?.SetDanger(ratio > 0.8f);
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

    private void HandleGourdUsed(CharacterBody c, int remaining)
    {
        if (c == playerBody)
        {
            itemSlotView?.SetGourdCount(remaining);
        }
    }

    private void HandleReviveAvailable(CharacterBody c)
    {
        if (c == playerBody)
        {
            // 回生次数-1（熄灭花瓣）+ 弹回生提示
            playerStatusView?.SetReviveDots(0);
            revivePromptView?.ShowPrompt();
        }
    }

    private void HandleRevived(CharacterBody c)
    {
        if (c == playerBody)
        {
            revivePromptView?.HidePrompt();
        }
    }

    private void HandleDeath(CharacterBody c)
    {
        if (c == playerBody)
        {
            revivePromptView?.HidePrompt();
            gameOverView?.ShowGameOver();
        }
    }

    private void HandleVictory(CharacterBody c)
    {
        if (c == bossBody)
        {
            victoryView?.ShowVictory();
        }
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
        perilousWarningView?.ShowWarning(type);
    }

    private void HandleFinisherTriggered(Vector3 pos)
    {
        // 处决表现由 AudioManager/FX 订阅处理，这里不需要
    }

    private void HandleLockOnChanged(bool isLocked)
    {
        lockOnIndicatorView?.SetLocked(isLocked);
    }
}
