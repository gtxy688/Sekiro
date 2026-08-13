using UnityEngine;

// MVC 之 Controller：订阅 CombatEventBus，把战斗数值转给对应 View
// 只做"数据 → View"的转发，不持有业务逻辑、不做每帧轮询
public class CombatUIController : MonoBehaviour
{
    [Header("角色引用（用于区分玩家/Boss）")]
    [SerializeField] private CharacterBody playerBody;
    [SerializeField] private CharacterBody bossBody;

    [Header("View 引用")]
    [SerializeField] private BossStatusView bossStatusView;        // 左上：红点+血条+名称
    [SerializeField] private BossPostureBarView bossPostureBarView; // 顶部：Boss 架势条
    [SerializeField] private PlayerStatusView playerStatusView;     // 玩家血条+架势+回生
    [SerializeField] private ItemSlotView itemSlotView;             // 右下：葫芦
    [SerializeField] private LockOnIndicatorView lockOnIndicatorView; // Boss 身上锁定点
    [SerializeField] private PerilousWarningView perilousWarningView; // "危"字

    // ===== 生命周期：订阅 / 取消订阅 =====
    private void OnEnable()
    {
        CombatEventBus.OnTakeDamage += HandleTakeDamage;
        CombatEventBus.OnHPChanged += HandleHPChanged;
        CombatEventBus.OnPostureChanged += HandlePostureChanged;
        CombatEventBus.OnPostureBroken += HandlePostureBroken;
        CombatEventBus.OnGourdUsed += HandleGourdUsed;
        CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
        CombatEventBus.OnPerilousAttack += HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered += HandleFinisherTriggered;
    }

    private void OnDisable()
    {
        CombatEventBus.OnTakeDamage -= HandleTakeDamage;
        CombatEventBus.OnHPChanged -= HandleHPChanged;
        CombatEventBus.OnPostureChanged -= HandlePostureChanged;
        CombatEventBus.OnPostureBroken -= HandlePostureBroken;
        CombatEventBus.OnGourdUsed -= HandleGourdUsed;
        CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
        CombatEventBus.OnPerilousAttack -= HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered -= HandleFinisherTriggered;
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

        // 初始状态：显示 Boss 名称
        bossStatusView?.SetName("苇名弦一郎");
    }

    // ===== 事件处理 =====

    // 血条：根据 victim 判断是玩家还是 Boss，转发给对应 View
    private void HandleTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        // 老事件（无 maxHp），用 OnHPChanged 那个带完整数据的
    }

    private void HandleHPChanged(CharacterBody c, int currentHp, int maxHp)
    {
        float ratio = (float)currentHp / maxHp;
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
        float ratio = posture / maxPosture;
        if (c == playerBody)
        {
            playerStatusView?.SetPosture(ratio);
        }
        else if (c == bossBody)
        {
            bossPostureBarView?.SetPosture(ratio);
            bossPostureBarView?.SetDanger(ratio > 0.8f);
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
        // 玩家死亡 → 弹回生提示（M14 接 UI）
    }

    private void HandlePerilousAttack(PerilousType type)
    {
        perilousWarningView?.ShowWarning(type);
    }

    private void HandleFinisherTriggered(Vector3 pos)
    {
        // 处决完成 → 忍杀灯熄灭一个（M10 接）
        // bossStatusView?.SetLifeDots(...)
    }
}
