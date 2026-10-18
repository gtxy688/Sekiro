using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.Body;
namespace ARPG.FrameWork.States
{
    // 玩家或 AI 离散动作的统一路由接口。
    public interface ICommand { }

    // 受击语境用于选择 CharacterConfig 中的动画；未配置时逐级回退到普通受击。
    public enum HurtContext
    {
        Normal,      // 裸吃普通攻击
        Heavy,       // 强力招式（击退更强/击飞）
        Guard,       // 格挡轻攻击
        GuardHeavy,  // 格挡重攻击（Knockback > 0）
        Deflected    // 被完美弹反后的硬直（攻击者播）
    }

    // 架势崩解来源决定等待姿态与忍杀演出，不能只看 IsPostureBroken。
    public enum PostureBreakSource
    {
        Attack,
        Deflect,
        Mikiri
    }

    // 普通、弹反和识破忍杀分别使用配套动画。
    public enum FinisherKind
    {
        Ground,
        Deflect,
        Mikiri
    }

    // CharacterBody.ReceiveHit 打包的命中快照，沿父子状态路由决定是否拦截或结算。
    public struct HitData
    {
        public CharacterBody attacker;    // 攻击者
        public int healthDmg;             // 血量伤害
        public float postureDmg;          // 架势伤害
        public Vector3 hitPoint;          // 命中点（打铁火花/音效定位）
        public bool isPerilous;           // 是否危字攻击
        public PerilousType perilousType; // 危字类型
        public float knockback;           // 击退强度（0=普通受击，>0 触发 Heavy 受击表现）
        public HitGrade hitGrade;         // 打到玩家时的受击等级；Boss 被打仍看 knockback
        public bool isProjectile;         // 箭走这条，用来区分箭 Heavy 的格挡/弹反
    }

    public struct IdleCommand : ICommand { }
    public struct MoveCommand : ICommand
    {
        public Vector2 Direction; 
        public MoveCommand(Vector2 dir) { Direction = dir; }
    }
    public struct JumpCommand : ICommand { }

    // 普通攻击/连招专用。不能因目标之后进入崩解而被重解释为忍杀。
    public struct AttackCommand : ICommand { }

    // 玩家在已有可执行忍杀机会时新按攻击键产生；与连招预输入分开，避免旧攻击缓冲自动处决。
    public struct FinisherCommand : ICommand { }

    // 真实按下携带身份并可开启弹反窗口；系统请求只恢复普通防御，不记空按。
    public enum DeflectCommandSource
    {
        // 默认值 0 表示系统回防，避免无参构造伪造一次真实按下。
        SystemRequest = 0,
        PhysicalPress = 1,
    }

    // PressId 是真实按下的递增身份：缓冲重试、状态重入都沿用同一个 ID，
    // 身体侧据此保证"同一次按下只开一次窗口"。系统请求的 PressId = 0。
    public struct DeflectCommand : ICommand
    {
        public int PressId;
        public DeflectCommandSource Source;

        public DeflectCommand(int pressId, DeflectCommandSource source)
        {
            PressId = pressId;
            Source = source;
        }

        // 玩家真实物理按下：唯一会生成 PressId 的入口在 PlayerBrain。
        public static DeflectCommand Physical(int pressId)
            => new DeflectCommand(pressId, DeflectCommandSource.PhysicalPress);

        // 系统请求防御姿态（崩解恢复自动回防、UI 防御选择）。
        public static DeflectCommand System() => new DeflectCommand(0, DeflectCommandSource.SystemRequest);

        // 能否开启主动弹反尝试：来源必须是物理按下，且带有效身份。
        // 实际是否开启还要看身体侧是否接受（缓冲过期、状态拒绝都不算）。
        public bool IsPhysicalPress
            => Source == DeflectCommandSource.PhysicalPress && PressId > 0;
    }

    // M6 新增：葫芦 / 锁定 / 闪避
    public struct HealCommand : ICommand { }
    public struct LockOnCommand : ICommand { }
    public struct DodgeCommand : ICommand { }
}
